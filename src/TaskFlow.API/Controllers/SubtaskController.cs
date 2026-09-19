using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/subtasks")]
[Authorize]
public class SubtasksController(ISubtaskService subtaskService) : ControllerBase
{
    private readonly ISubtaskService _subtaskService = subtaskService;

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

    [HttpGet("task/{taskId:guid}")]
    public async Task<IActionResult> GetByTask(Guid taskId)
    {
        var subtasks =
            await _subtaskService.GetByTaskAsync(
                taskId,
                CurrentUserId
            );

        return Ok(subtasks);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var subtask =
            await _subtaskService.GetByIdAsync(
                id,
                CurrentUserId
            );

        return Ok(subtask);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        CreateSubtaskRequest request
    )
    {
        var subtask =
            await _subtaskService.CreateAsync(
                request,
                CurrentUserId
            );

        return CreatedAtAction(
            nameof(GetById),
            new { id = subtask.Id },
            subtask
        );
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody]
        UpdateSubtaskRequest request
    )
    {
        var subtask =
            await _subtaskService.UpdateAsync(
                id,
                CurrentUserId,
                request
            );

        return Ok(subtask);
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(
        Guid id
    )
    {
        var subtask =
            await _subtaskService.ToggleAsync(
                id,
                CurrentUserId
            );

        return Ok(subtask);
    }

    [HttpPut("task/{taskId:guid}/reorder")]
    public async Task<IActionResult> Reorder(
        Guid taskId,
        [FromBody]
        ReorderSubtasksRequest request
    )
    {
        if (request.TaskId != taskId)
        {
            return BadRequest(
                new { message = "TaskId in route and body must match" }
            );
        }

        var subtasks =
            await _subtaskService.ReorderAsync(
                request,
                CurrentUserId
            );

        return Ok(subtasks);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _subtaskService.DeleteAsync(
            id,
            CurrentUserId
        );

        return NoContent();
    }
}
