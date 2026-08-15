using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Auth.DTOs;

[ApiController]
[Route("api/me")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public UserController(IUserService service)
    {
        _service = service;
    }
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var user = await _service.GetProfileAsync(CurrentUserId);
            return Ok(user);
        }
        catch (Exception exception)
        {
            return BadRequest(
                new
                {
                    message = exception.Message
                }
            );
        }
    }
    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UserDto userRequest
    )
    {
        try
        {
            var result = await _service.UpdateAsync(userRequest);
            return Ok(result);
        }
        catch (Exception exception)
        {
            return BadRequest(
                new
                {
                    message = exception.Message
                }
            );
        }
    }
}