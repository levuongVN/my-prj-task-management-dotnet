using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

}