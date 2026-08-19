using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Interfaces;

[ApiController]
[Route("api/me")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _service;
    private readonly IDeviceService _deviceService;

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

    private Guid CurrentDeviceId =>
        Guid.TryParse(
            User.FindFirstValue("device_id"),
            out var deviceId
        )
            ? deviceId
            : Guid.Empty;

    public UserController(IUserService service, IDeviceService deviceService)
    {
        _service = service;
        _deviceService = deviceService;
    }


    // GET /api/me
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _service.GetProfileAsync(
            CurrentUserId
        );

        return Ok(user);
    }


    // PUT /api/me/update
    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UserDto userRequest
    )
    {
        // Không sử dụng Id FE gửi lên
        userRequest.Id = CurrentUserId;

        var result = await _service.UpdateAsync(
            userRequest
        );

        return Ok(result);
    }


    // PUT /api/me/update/password
    [HttpPut("update/password")]
    public async Task<IActionResult> UpdatePassword(
        [FromBody] UpdatePasswordRequest request
    )
    {
        var user = new UserDto
        {
            Id = CurrentUserId
        };

        var result = await _service.UpdatePasswordAsync(
                user,
                request.CurrentPassword,
                request.NewPassword
            );

        return Ok(result);
    }


    // POST /api/me/avatar
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(
        [FromForm] IFormFile avatar
    )
    {
        if (avatar == null)
        {
            throw new ArgumentException(
                "Avatar file is required."
            );
        }

        await using var stream = avatar.OpenReadStream();

        var fileDto = new FileUploadDto
        {
            Stream = stream,
            FileName = avatar.FileName,
            ContentType = avatar.ContentType,
            Length = avatar.Length
        };

        var result = await _service.UploadAvatarAsync(
                CurrentUserId,
                fileDto
            );

        return Ok(result);
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        var result =
            await _service.DeleteAvatarAsync(
                CurrentUserId
            );

        return Ok(result);
    }

    // GET /api/me/devices
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await _deviceService.GetUserDevicesAsync(
            CurrentUserId,
            CurrentDeviceId
        );

        return Ok(devices);
    }

    // DELETE /api/me/devices/{deviceId}
    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> RevokeDevice(Guid deviceId)
    {
        await _deviceService.RevokeDeviceAsync(
            CurrentUserId,
            deviceId
        );

        return Ok(new { message = "Device logged out successfully" });
    }
}