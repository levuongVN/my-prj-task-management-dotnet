using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Comments.DTOs;
using TaskFlow.Application.Features.Comments.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/comments")]
[Authorize]
public class CommentsController(
    ICommentService commentService
) : ControllerBase
{
    private readonly ICommentService _commentService = commentService;

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

    [HttpGet("task/{taskId:guid}")]
    public async Task<IActionResult> GetByTask(Guid taskId)
    {
        var comments = await _commentService.GetByTaskIdAsync(taskId, CurrentUserId);
        return Ok(comments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var comment = await _commentService.GetByIdAsync(id, CurrentUserId);
        return Ok(comment);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCommentRequest request
    )
    {
        var comment = await _commentService.CreateAsync(CurrentUserId, request);
        return CreatedAtAction(nameof(GetById), new { id = comment.Id }, comment);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCommentRequest request
    )
    {
        var comment = await _commentService.UpdateAsync(id, CurrentUserId, request);
        return Ok(comment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _commentService.DeleteAsync(id, CurrentUserId);
        return NoContent();
    }
}
