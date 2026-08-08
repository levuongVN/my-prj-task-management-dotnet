using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Meetings.DTOs;
using TaskFlow.Application.Features.Meetings.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController(
    IMeetingService meetingService
) : ControllerBase
{
    private readonly IMeetingService _meetingService = meetingService;

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var meetings = await _meetingService.GetAllAsync(CurrentUserId);
        return Ok(meetings);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var meeting = await _meetingService.GetByIdAsync(id, CurrentUserId);
        return Ok(meeting);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrUpdateMeetingRequest request
    )
    {
        var meeting = await _meetingService.CreateAsync(request, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, meeting);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateOrUpdateMeetingRequest request
    )
    {
        var meeting = await _meetingService.UpdateAsync(id, request, CurrentUserId);
        return Ok(meeting);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _meetingService.DeleteAsync(id, CurrentUserId);
        return NoContent();
    }
}