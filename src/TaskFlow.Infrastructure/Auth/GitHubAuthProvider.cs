using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Infrastructure.Auth;

/// <summary>
/// Nhiệm vụ: đổi authorization code (FE thu được sau redirect GitHub) thành
/// dữ liệu user. Vì GitHub KHÔNG có ID token như Google, bắt buộc đi qua
/// Authorization Code Flow:
///
///     [FE] redirect github.com/login/oauth/authorize
///        -> user bấm Allow
///        -> GitHub redirect về FE: /auth/github/callback?code=xxx
///     [BE] POST code + ClientSecret  ->  access_token   (bước này)
///          GET /user + /user/emails  ->  email/name/avatar
///
/// Secret phải nằm ở BE (appsettings, gitignored): nếu ở FE thì ai view
/// source cũng thấy và có thể tự exchange code lấy quyền user.
/// </summary>
public class GitHubAuthProvider : IGitHubAuthProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GitHubAuthProvider(HttpClient httpClient, IConfiguration configuration)
    {
        // HttpClient inject từ IHttpClientFactory (đăng ký typed client trong DI)
        // - tự quản lý connection pooling, không cần new trong code
        _httpClient = httpClient;
        _clientId = configuration["Authentication:GitHub:ClientId"] ?? string.Empty;
        _clientSecret = configuration["Authentication:GitHub:ClientSecret"] ?? string.Empty;
    }

    // Chain chính: code -> access token -> user info
    public async Task<ExternalUserInfo> ExchangeCodeAsync(string code)
    {
        var accessToken = await ExchangeCodeForTokenAsync(code);

        return await GetUserInfoAsync(accessToken);
    }

    // ===== BƯỚC 1: code + secret -> access token =====
    // GitHub phát 'code' qua redirect về FE, nhưng CHỈ thấy code chưa đăng nhập
    // được (code dùng 1 lần + ngắn hạn + phải ghép secret mới có token).
    private async Task<string> ExchangeCodeForTokenAsync(string code)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            new
            {
                client_id = _clientId,
                client_secret = _clientSecret,
                code,

                // GitHub kiểm tra redirect_uri bước exchange phải KHỚP callback
                // đã khai khi tạo OAuth App - chống attacker chuyển hướng code về chỗ khác
                redirect_uri = "http://localhost:5173/auth/github/callback"
            }
        );

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // code sai/hết hạn/dùng lại -> GitHub trả error, ta gom về 1 message chung
            throw new Exception("GitHub authorization failed");
        }

        using var json = JsonDocument.Parse(content);

        var tokenElement = json.RootElement.GetProperty("access_token");

        return tokenElement.GetString() ?? throw new Exception("GitHub access token is empty");
    }

    // ===== BƯỚC 2: access token -> thông tin user =====
    private async Task<ExternalUserInfo> GetUserInfoAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );

        // GitHub API bắt buộc User-Agent: request thiếu header này bị 403
        request.Headers.UserAgent.ParseAdd("TaskFlow");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch GitHub user info");
        }

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync()
        );

        var root = json.RootElement;

        // Email PUBLIC trên GitHub có thể null/rỗng (user bật "Keep my email
        // addresses private") -> không thể match account -> fallthrough xuống /user/emails
        var email = root.TryGetProperty("email", out var emailProp)
            ? emailProp.GetString() : null;

        if (string.IsNullOrEmpty(email))
        {
            email = await GetPrimaryEmailAsync(accessToken);
        }

        // không có email được verified nào -> GẤP cho đăng nhập.
        // Thà fail chứ không tạo user với email rác (làm ô nhiễm unique index email)
        if (string.IsNullOrEmpty(email))
        {
            throw new Exception("GitHub account does not have a public email");
        }

        return new ExternalUserInfo
        {
            Email = email,
            FullName = root.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null,
            AvatarUrl = root.TryGetProperty("avatar_url", out var avatarProp)
                ? avatarProp.GetString()
                : null
        };
    }

    // Lấy email chính (primary + verified) - đây là email 'thật' dù user ẩn public email
    private async Task<string?> GetPrimaryEmailAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user/emails"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        request.Headers.UserAgent.ParseAdd("TaskFlow");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync()
        );

        // /user/emails trả mảng: email, primary, verified...
        // Danh sách có thể có nhiều email, ta CHỈ lấy primary + verified:
        //   - primary: email chính, đại diện danh tính user
        //   - verified: đã từng xác nhận để chắc chắn user sở hữu
        foreach (var item in json.RootElement.EnumerateArray())
        {
            var isPrimary = item.TryGetProperty("primary", out var p) && p.GetBoolean();
            var isVerified = item.TryGetProperty("verified", out var v) && v.GetBoolean();

            if (isPrimary && isVerified)
            {
                return item.GetProperty("email").GetString();
            }
        }

        return null;
    }
}
