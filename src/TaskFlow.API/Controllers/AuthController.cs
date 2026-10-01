using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wangkanai.Detection.Services;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Features.Auth.Interfaces;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{

    private readonly IAuthService _authService;
    private readonly IDetectionService _detectionService;

    public AuthController(IAuthService authService, IDetectionService detectionService)
    {
        _authService = authService;
        _detectionService = detectionService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request
    )
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var deviceType = _detectionService.Device.Type.ToString();
            var browserName = _detectionService.Browser.Name.ToString();
            var platformName = _detectionService.Platform.Name.ToString();
            var deviceName = $"{browserName} on {platformName}";

            var response = await _authService.Login(request, ipAddress, deviceType, deviceName);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request
    )
    {
        try
        {
            var response = await _authService.RefreshToken(request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // HANDOFF: 2 endpoint OAuth là "anonymous" (không [Authorize]) như login thường,
    // vì người dùng CHƯA có session khi đăng nhập. Endpoint không tự verify
    // anything - vai trò verify nằm ở GoogleAuthProvider/GitHubAuthProvider, controller
    // chỉ làm: nhận request -> thu thập metadata từ environment (IP/browser/device) -> ủy cho AuthService

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Register cùng semantics login: đăng ký xong nhận ngay access + refresh token
        return await ExecuteAuthAction(() =>
            _authService.Register(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                _detectionService.Device.Type.ToString(),
                $"{_detectionService.Browser.Name} on {_detectionService.Platform.Name}"
            )
        );
    }

    [HttpPost("google")]
    public async Task<IActionResult> LoginWithGoogle(GoogleLoginRequest request)
    {
        // FE gửi ID token do Google trả về, BE verify trước khi tin email
        // (Wangkanai.Detection tự bắt DeviceType/Browser/Platform từ User-Agent
        //  -> gán đủ 'device info' cho OAuth session giống hệt login thường)
        return await ExecuteAuthAction(() =>
            _authService.LoginWithGoogle(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                _detectionService.Device.Type.ToString(),
                $"{_detectionService.Browser.Name} on {_detectionService.Platform.Name}"
            )
        );
    }

    [HttpPost("github")]
    public async Task<IActionResult> LoginWithGitHub(GithubLoginRequest request)
    {
        // FE gửi authorization code (dấu hiệu GitHub redirect về), BE tự exchange
        return await ExecuteAuthAction(() =>
            _authService.LoginWithGitHub(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                _detectionService.Device.Type.ToString(),
                $"{_detectionService.Browser.Name} on {_detectionService.Platform.Name}"
            )
        );
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request
    )
    {
        // Idempotent: token không tồn tại/hết hạn vẫn trả 200
        // để FE luôn dọn localStorage và redirect về login
        try
        {
            await _authService.Logout(request);

            return Ok(new
            {
                message = "Logged out successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // Anonymous như login: user CHƯA có session khi quên mật khẩu.
    // Luôn trả message chung kể cả email không tồn tại - không dò được account
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request
    )
    {
        try
        {
            await _authService.ForgotPassword(request);

            return Ok(new
            {
                message = "If that email exists, a password reset link has been sent"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request
    )
    {
        try
        {
            await _authService.ResetPassword(request);

            return Ok(new
            {
                message = "Password has been reset successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // Email verification là flow "soft": response giống nhau kể cả email nào
    // (resend-verification bắt buộc luôn 200) - không cho dò email nào có account
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request
    )
    {
        try
        {
            await _authService.VerifyEmail(request);

            return Ok(new
            {
                message = "Email verified successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request
    )
    {
        try
        {
            // Luôn trả message chung - nếu email chưa verify mail mới được gửi đi,
            // nếu không thì im lặng như forgot-password (chống dò account)
            await _authService.ResendVerificationEmail(request);

            return Ok(new
            {
                message = "If the email needs verification, a new link has been sent"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // Các luồng login dùng chung xử lý lỗi: exception -> 400 { message }
    // (giữ đúng format error response của endpoints login cũ)
    private async Task<IActionResult> ExecuteAuthAction(Func<Task<AuthResponse>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}