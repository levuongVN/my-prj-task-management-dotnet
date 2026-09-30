using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Extensions;
using TaskFlow.Application.Features.Tasks.DTOs;
using TaskFlow.Application.Features.Tasks.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController(ITaskService taskService) : ControllerBase{
    private readonly ITaskService _taskService = taskService;

    private Guid CurrentUserId => User.GetUserId();
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id
    )
    {
        var task =
            await _taskService.GetByIdAsync(
                id,
                CurrentUserId
            );

        return Ok(task);
    }
    [HttpGet("project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        var tasks =
            await _taskService
                .GetPagedProjectTasksAsync(
                    projectId,
                    CurrentUserId,
                    page,
                    pageSize
                );

        return Ok(tasks);
    }
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody]
        CreateOrUpdateTaskRequest request
    )
    {
        var task =
            await _taskService.CreateAsync(
                request,
                CurrentUserId
            );

        return CreatedAtAction(
            nameof(GetById),
            new { id = task.Id },
            task
        );
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody]CreateOrUpdateTaskRequest request
    )
    {
        var task =
            await _taskService.UpdateAsync(
                id,
                CurrentUserId,
                request
            );

        return Ok(task);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id
    )
    {
        await _taskService.DeleteAsync(
            id,
            CurrentUserId
        );

        return NoContent();
    }

    // ?labelId= optional - server-side filter theo label, totalCount đúng cho pagination
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? labelId = null
    )
    {
        var tasks =
            await _taskService.GetPagedByUserAsync(
                CurrentUserId,
                page,
                pageSize,
                labelId
            );

        return Ok(tasks);
    }
}