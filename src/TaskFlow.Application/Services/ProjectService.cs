using TaskFlow.Application.DTOs.Projects;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;

    private static int CalculateProgress(Project project)
    {
        var total = project.Tasks.Count;

        if (total == 0)
            return 0;

        var done = project.Tasks.Count(
            x => x.Status == Domain.Enums.TaskStatus.Done
        );

        return done * 100 / total;
    }

    public ProjectService(
        IProjectRepository projectRepository
    )
    {
        _projectRepository = projectRepository;
    }

    public async Task<List<ProjectResponse>> GetAllAsync(
        Guid userId
    )
    {
        var projects = await _projectRepository.GetAllByUserAsync(userId);

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        Guid userId
    )
    {
        var project =
            await _projectRepository.GetByIdAsync(
                projectId,
                userId
            );

        return project is null
            ? null
            : MapToResponse(project);
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateOrUpdateProjectRequest request,
        Guid userId
    )
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Due = request.Due,
            Status = request.Status,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _projectRepository.AddAsync(project);
        await _projectRepository.SaveChangesAsync();

        return MapToResponse(project);
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid projectId,
        CreateOrUpdateProjectRequest request,
        Guid userId
    )
    {
        var project =
            await _projectRepository.GetByIdAsync(
                projectId,
                userId
            );

        if (project is null)
        {
            throw new Exception("Project not found");
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.Due = request.Due;
        project.Status = request.Status;
        project.UpdatedAt = DateTime.UtcNow;

        _projectRepository.Update(project);
        await _projectRepository.SaveChangesAsync();

        return MapToResponse(project);
    }

    public async Task DeleteAsync(
        Guid projectId,
        Guid userId
    )
    {
        var project =
            await _projectRepository.GetByIdAsync(
                projectId,
                userId
            );

        if (project is null)
        {
            throw new Exception("Project not found");
        }

        project.IsDeleted = true;

        _projectRepository.Update(project);
        await _projectRepository.SaveChangesAsync();
    }

    private static ProjectResponse MapToResponse(
        Project project
    )
    {
        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Due = project.Due,
            Status = project.Status,
            CreatedAt = project.CreatedAt,
            Progress = CalculateProgress(project)
        };
    }
}