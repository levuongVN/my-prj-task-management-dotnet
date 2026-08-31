using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Notifications.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(
        INotificationService notificationService
    )
    {
        _notificationService = notificationService;
    }

    private Guid UserId =>
        Guid.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )!
        );

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery]
        int take = 50
    )
    {
        var notifications = await _notificationService
            .GetByUserIdAsync(UserId, take);

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService
            .CountUnreadAsync(UserId);

        return Ok(new { count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var marked = await _notificationService
            .MarkAsReadAsync(UserId, id);

        if (!marked)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var count = await _notificationService
            .MarkAllAsReadAsync(UserId);

        return Ok(new { count });
    }
}
