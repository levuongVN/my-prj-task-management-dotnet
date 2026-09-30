using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Extensions;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/labels")]
[Authorize]
public class LabelController : ControllerBase
{
    private readonly ILabelService _labelService;

    public LabelController(ILabelService labelService)
    {
        _labelService = labelService;
    }

    private Guid CurrentUserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var labels = await _labelService.GetAllAsync(CurrentUserId);

        return Ok(labels);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] LabelRequest request
    )
    {
        var label = await _labelService.CreateAsync(
            CurrentUserId,
            request
        );

        return Created($"/api/labels/{label.Id}", label);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] LabelRequest request
    )
    {
        var label = await _labelService.UpdateAsync(
            id,
            CurrentUserId,
            request
        );

        return Ok(label);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id
    )
    {
        await _labelService.DeleteAsync(
            id,
            CurrentUserId
        );

        return NoContent();
    }
}
