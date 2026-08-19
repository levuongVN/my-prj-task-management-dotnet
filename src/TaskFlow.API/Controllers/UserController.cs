using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Auth.DTOs;

[ApiController]
[Route("api/me")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _service;

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

    public UserController(IUserService service)
    {
        _service = service;
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
}