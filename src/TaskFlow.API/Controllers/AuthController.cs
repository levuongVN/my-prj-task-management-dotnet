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
}