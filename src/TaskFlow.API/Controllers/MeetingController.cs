using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Extensions;
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

    private Guid CurrentUserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        var meetings = await _meetingService.GetPagedAsync(
            CurrentUserId,
            page,
            pageSize
        );
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