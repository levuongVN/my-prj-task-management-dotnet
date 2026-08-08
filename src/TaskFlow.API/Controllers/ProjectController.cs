using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.DTOs.Projects;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : Controller
{
    private readonly IProjectService _projectService;
    protected Guid UserId => Guid.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )!
        );
    public ProjectsController(
        IProjectService projectService
    )
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectService.GetAllAsync(
            UserId
        );

        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id
    )
    {
        var project = await _projectService.GetByIdAsync(
            id,
            UserId
        );

        if (project is null)
        {
            return NotFound(
                new
                {
                    message = "Project not found"
                }
            );
        }

        return Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrUpdateProjectRequest request
    )
    {
        var project = await _projectService.CreateAsync(
            request,
            UserId
        );

        return CreatedAtAction(
            nameof(GetById),
            new { id = project.Id },
            project
        );
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        CreateOrUpdateProjectRequest request
    )
    {
        var project =
            await _projectService.UpdateAsync(
                id,
                request,
                UserId
            );

        return Ok(project);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id
    )
    {
        await _projectService.DeleteAsync(
            id,
            UserId
        );

        return NoContent();
    }
}