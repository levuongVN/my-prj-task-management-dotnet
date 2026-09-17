using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Wangkanai.Detection.Services;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Features.Auth.Services;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{

    private readonly AuthService _authService;
    private readonly IDetectionService _detectionService;

    public AuthController(AuthService authService, IDetectionService detectionService)
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