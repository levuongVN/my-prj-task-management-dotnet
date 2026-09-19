using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Emails;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Features.Auth.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Auth.Services;

public class AuthService(
    IJwtTokenGenerator jwtTokenGenerator,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDeviceService deviceService,
    IGoogleAuthProvider googleAuthProvider,
    IGitHubAuthProvider gitHubAuthProvider,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IEmailSender emailSender,
    IConfiguration configuration
) : IAuthService
{
    // Thời gian sống của link reset password trong email (phút)
    private const int ResetTokenExpiryMinutes = 15;

    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IGoogleAuthProvider _googleAuthProvider = googleAuthProvider;
    private readonly IGitHubAuthProvider _gitHubAuthProvider = gitHubAuthProvider;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = passwordResetTokenRepository;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IConfiguration _configuration = configuration;

    public async Task<AuthResponse> Login(
    LoginRequest request,
    string? ipAddress = null,
    string? deviceType = null,
    string? deviceName = null
)
    {
        var user = await _userRepository.GetByEmailAsync(
            request.Email
        );

        if (user == null)
        {
            // Lưu ý: message trả thẳng ra 400 - dev-only,
            // production nên đổi chung 1 message để không lộ user nào tồn tại
            throw new Exception("User not found");
        }

        // BCrypt hash là one-way: Verify(password nhập vào + hash lưu trong DB)
        // so khớp ngay trên hash. DB không bao giờ giữ password thô.
        var isValidPassword = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash
        );

        if (!isValidPassword)
        {
            throw new Exception("Invalid password");
        }

        // Phần upsert device + cấp token dùng chung với OAuth login
        return await IssueTokensAsync(
            user,
            request.Device,
            ipAddress,
            deviceType,
            deviceName
        );
    }

    /// <summary>
    /// Phần chung của mọi luồng đăng nhập (password / Google / GitHub):
    /// upsert device tracking -> sinh access token (JWT) -> tạo refresh token (7 ngày).
    /// Vì kết quả trả ra (AuthResponse) GIỐNG HỆT nhau ở cả 3 luồng nên FE
    /// không cần biết user login bằng cách nào - xử lý token như cũ.
    /// </summary>
    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        DeviceRequest? deviceRequest = null,
        string? ipAddress = null,
        string? deviceType = null,
        string? deviceName = null
    )
    {
        UserDevice? device = null;

        // BƯỚC 1: đăng ký/cập nhật device đang login.
        // FE gửi fingerprint (UUID cố định trong localStorage) -> BE sẽ:
        //   - fingerprint đã có  -> update LastLoginAt + IsActive = true (tái sử dụng)
        //   - fingerprint mới    -> tạo device mới (nếu đã đủ 3 device active -> chiếm chỗ device cũ nhất)
        // Device là chỗ gắn đời sống session: logout/revoke device sẽ giết toàn bộ refresh token của nó.
        if (deviceRequest != null)
        {
            device = await _deviceService.UpsertDeviceAsync(
                user.Id,
                deviceRequest,
                ipAddress,
                deviceType ?? "Unknown",
                deviceName ?? "Unknown device"
            );
        }

        // BƯỚC 2: access token - JWT sống 10 phút (Jwt:ExpiryMinutes), FE đính vào
        // claim device_id để middleware tracking và "Logout device" trong Settings
        // biết device nào là device đang dùng (IsCurrentDevice).
        var accessToken = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email,
            device?.Id ?? Guid.Empty
        );

        // BƯỚC 3: refresh token - 1 chuỗi random lưu DB, sống 7 ngày.
        // Access token hết hạn (10') -> FE POST /auth/refresh-token để lấy cái mới,
        // hạn lớn hơn access token => user không phải login lại giữa chừng.
        // isRevoked flag + gắn với device: logout/revoke device -> chết hết session.
        var refreshTokenString = GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            UserDeviceId = device?.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(
                refreshToken
            );

        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,

            RefreshToken = new RefreshTokenDto
            {
                Token = refreshToken.Token,
                ExpiresAt = refreshToken.ExpiresAt
            },

            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarPath,
                CreatedAt = user.CreatedAt
            }
        };
    }
    public async Task<AuthResponse> RefreshToken(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken
        );

        if (storedToken == null)
        {
            throw new Exception("Refresh token not found");
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow || storedToken.IsRevoked)
        {
            throw new Exception("Refresh token is expired or revoked");
        }

        if (storedToken.UserDevice != null && !storedToken.UserDevice.IsActive)
        {
            throw new Exception("Device has been logged out");
        }

        if (storedToken.UserDevice != null)
        {
            storedToken.UserDevice.LastActiveAt = DateTime.UtcNow;
        }

        return new AuthResponse
        {
            AccessToken = _jwtTokenGenerator.GenerateToken(
                storedToken.UserId,
                storedToken.User.Email,
                storedToken.UserDeviceId ?? Guid.Empty
            ),
            RefreshToken = new RefreshTokenDto
            {
                Token = storedToken.Token,
                ExpiresAt = storedToken.ExpiresAt
            },
            User = new UserDto
            {
                Id = storedToken.User.Id,
                Email = storedToken.User.Email,
                FullName = storedToken.User.FullName,
                AvatarUrl = storedToken.User.AvatarPath,
                CreatedAt = storedToken.User.CreatedAt
            }
        };
    }

    // FLOW GOOGLE (FE dùng Google Identity Services):
    //   user chọn account trên popup Google -> Google trả ID token (JWT do Google ký)
    //   -> FE POST /auth/google { idToken, device }
    //   -> BE VERIFY token (không bao giờ tin nguyên token JWT blind)
    //   -> lấy email/name -> GetOrCreateUser -> IssueTokens (như login thường)
    public async Task<AuthResponse> LoginWithGoogle(
        GoogleLoginRequest request,
        string? ipAddress = null,
        string? deviceType = null,
        string? deviceName = null
    )
    {
        // Verify ID token với Google: kiểm tra chữ ký, hạn dùng, audience (Client ID).
        // Token giả/hết hạn/token của app khác sẽ ném exception -> controller trả 400.
        // Nếu không verify, attacker có thể tự tạo token khai email của người khác
        // và đăng nhập vào account đó - đây là điểm bảo mật quan trọng nhất.
        var googleUser = await _googleAuthProvider.ValidateIdTokenAsync(
            request.IdToken
        );

        // Chưa verify thì chưa tin email, verify xong mới match/tạo user
        var user = await GetOrCreateUserAsync(
            googleUser.Email,
            googleUser.FullName
        );

        // Đã có user -> phát hành session đúng như login bằng password
        return await IssueTokensAsync(
            user,
            request.Device,
            ipAddress,
            deviceType,
            deviceName
        );
    }

    // FLOW GITHUB (FE chỉ redirect - code exchange phải làm ở BE):
    //   FE redirect sang github.com/login/oauth/authorize -> user Allow
    //   -> GitHub redirect về FE callback kèm ?code=xxx (code dùng 1 lần, chết trong vài phút)
    //   -> FE POST /auth/github { code, device }
    //   -> BE dùng code + CLIENT SECRET đổi access token - secret nằm ở BE, không bao giờ ở FE
    //   -> gọi GitHub API lấy email/name -> same flow như Google
    public async Task<AuthResponse> LoginWithGitHub(
        GithubLoginRequest request,
        string? ipAddress = null,
        string? deviceType = null,
        string? deviceName = null
    )
    {
        // Exchange code -> access token -> user info, xem chi tiết trong GitHubAuthProvider
        var githubUser = await _gitHubAuthProvider.ExchangeCodeAsync(
            request.Code
        );

        var user = await GetOrCreateUserAsync(
            githubUser.Email,
            githubUser.FullName
        );

        return await IssueTokensAsync(
            user,
            request.Device,
            ipAddress,
            deviceType,
            deviceName
        );
    }

    /// <summary>
    /// OAuth không có mật khẩu: email đã tồn tại -> dùng luôn account đó,
    /// email mới -> tự tạo user (PasswordHash rỗng, chỉ login bằng provider).
    /// Email lấy trực tiếp từ Google/GitHub (đã verified phía provider)
    /// nên được coi là "chắc chắn thuộc người vừa login" -> đủ tin để match.
    /// </summary>
    private async Task<User> GetOrCreateUserAsync(
        string email,
        string? fullName
    )
    {
        var user = await _userRepository.GetByEmailAsync(email);

        // CASE 1: email đã có account (từ trước, có thể là password session)
        // -> login thẳng vào account cũ, không đụng password
        if (user != null)
        {
            return user;
        }

        // CASE 2: email chưa có -> tự tạo user (auto-provisioning),
        // không có trang Register riêng cho OAuth
        var newUser = new User
        {
            Email = email,

            // OAuth user không đăng nhập bằng password -> để rỗng (cột string
            // non-nullable). BCrypt.Verify luôn fail nếu ai thử đăng nhập thường.
            PasswordHash = string.Empty,

            // Provider có thể không trả tên (GitHub name nullable)
            // -> fallback về email cho thỏa unique requirement
            FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName,

            // KHÔNG lưu avatarUrl của provider vào AvatarPath: cột này dành cho
            // PATH sau khi upload lên Supabase (GetProfileAsync sẽ tạo signed URL
            // từ path đó, dính foreign URL sẽ ra URL sai) -> để null,
            // FE tự fallback hiển thị chữ cái đầu
            AvatarPath = null
        };

        return await _userRepository.AddAsync(newUser);
    }

    private string GenerateRefreshToken()
    {
        return Guid.NewGuid().ToString() +
               Guid.NewGuid().ToString();
    }

    public async Task Logout(LogoutRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken
        );

        if (storedToken == null || storedToken.IsRevoked)
        {
            // Idempotent: logout lại nhiều lần vẫn thành công
            return;
        }

        if (storedToken.UserDeviceId != null)
        {
            // Revoke toàn bộ refresh token của device + deactivate device
            // (tab khác cùng browser cũng bị đăng xuất)
            await _deviceService.RevokeDeviceAsync(
                storedToken.UserId,
                storedToken.UserDeviceId.Value
            );
        }

        storedToken.IsRevoked = true;
        await _refreshTokenRepository.SaveChangesAsync();
    }

    // FLOW FORGOT PASSWORD:
    //   user quên mật khẩu -> POST /auth/forgot-password { email }
    //   -> BE tạo token random 32 bytes (raw chỉ nằm trong link email, DB giữ hash)
    //   -> gửi email HTML chứa link {FrontendUrl}/reset-password?token=xxx
    // API luôn trả 200 kể cả email không tồn tại -> attacker không dò được
    // account nào đang có trong hệ thống qua endpoint này (user enumeration)
    public async Task ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null)
        {
            return;
        }

        // Yêu cầu gửi lại (= invalidate token cũ) chỉ token mới nhất còn hiệu lực
        await _passwordResetTokenRepository.InvalidateUserTokensAsync(user.Id);

        var rawToken = GenerateRawResetToken();

        var passwordResetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(ResetTokenExpiryMinutes)
        };

        await _passwordResetTokenRepository.AddAsync(passwordResetToken);
        await _passwordResetTokenRepository.SaveChangesAsync();

        var resetLink = $"{_configuration["App:FrontendUrl"]}/reset-password?token={rawToken}";

        await _emailSender.SendAsync(
            user.Email,
            AuthEmailTemplates.ForgotPasswordSubject(),
            AuthEmailTemplates.ForgotPasswordHtml(user.FullName, resetLink, ResetTokenExpiryMinutes)
        );
    }

    // FLOW RESET PASSWORD:
    //   user mở link trong email -> FE cho nhập mật khẩu mới
    //   -> POST /auth/reset-password { token, newPassword }
    //   -> BE hash token trong link, truy ngược PasswordResetToken trong DB
    //   -> verify (chưa dùng, chưa hết hạn) -> đổi password + chết hết session cũ
    public async Task ResetPassword(ResetPasswordRequest request)
    {
        var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(
            ComputeTokenHash(request.Token)
        );

        var isInvalid =
            resetToken == null ||
            resetToken.UsedAt != null ||
            resetToken.ExpiresAt < DateTime.UtcNow;

        if (isInvalid)
        {
            throw new Exception("Reset token is invalid or expired");
        }

        var user = resetToken!.User;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        resetToken.UsedAt = DateTime.UtcNow;

        // Password mới -> mọi session đang giữ refresh token đều bị đóng,
        // access token đang live chỉ còn tối đa 10' (Jwt:ExpiryMinutes) rồi tự chết
        await _refreshTokenRepository.RevokeByUserAsync(user.Id);

        await _passwordResetTokenRepository.SaveChangesAsync();
    }

    private static string GenerateRawResetToken()
    {
        // 32 bytes ngẫu nhiên từ CSPRNG -> base64url an toàn trong URL query,
        // khoảng entropy 256-bit -> không thể đoán/thu được bằng brute force
        return System.Buffers.Text.Base64Url.EncodeToString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)
        );
    }

    private static string ComputeTokenHash(string rawToken)
    {
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawToken)
            )
        );
    }
}
