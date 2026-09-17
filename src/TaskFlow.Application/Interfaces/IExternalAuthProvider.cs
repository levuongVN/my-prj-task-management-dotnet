namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Thông tin user Google/GitHub trả về sau khi verify identity,
/// FE sẵn để AuthService mapping vào entity User nội bộ.
/// </summary>
public class ExternalUserInfo
{
    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }
}

public interface IGoogleAuthProvider
{
    /// <summary>Verify Google ID token (check chữ ký + audience) và trích thông tin user.</summary>
    Task<ExternalUserInfo> ValidateIdTokenAsync(string idToken);
}

public interface IGitHubAuthProvider
{
    /// <summary>Đổi authorization code lấy access token rồi gọi GitHub API lấy thông tin user.</summary>
    Task<ExternalUserInfo> ExchangeCodeAsync(string code);
}
