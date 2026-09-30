using TaskFlow.Application.Common;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Projects;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;

    private readonly ILabelRepository _labelRepository;

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
        IProjectRepository projectRepository,
        ILabelRepository labelRepository
    )
    {
        _projectRepository = projectRepository;
        _labelRepository = labelRepository;
    }

    public async Task<List<ProjectResponse>> GetAllAsync(
        Guid userId
    )
    {
        var projects = await _projectRepository.GetAllByUserAsync(userId);

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<PagedResult<ProjectResponse>> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? labelId = null
    )
    {
        (page, pageSize) = PagedResult<ProjectResponse>.Normalize(page, pageSize);

        var (projects, totalCount) = await _projectRepository.GetPagedByUserAsync(
            userId,
            page,
            pageSize,
            labelId
        );

        return new PagedResult<ProjectResponse>
        {
            Items = projects.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
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

        // labelIds null/[] = không gán label nào khi tạo
        if (request.LabelIds != null)
        {
            project.Labels = await ResolveLabelsAsync(userId, request.LabelIds);
        }

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
            throw new NotFoundException("Project not found");
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.Due = request.Due;
        project.Status = request.Status;
        project.UpdatedAt = DateTime.UtcNow;

        // labelIds == null -> không đụng labels, giữ nguyên quan hệ
        if (request.LabelIds != null)
        {
            project.Labels = await ResolveLabelsAsync(userId, request.LabelIds);
        }

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
            throw new NotFoundException("Project not found");
        }

        project.IsDeleted = true;

        _projectRepository.Update(project);
        await _projectRepository.SaveChangesAsync();
    }

    // Gún labels vào project: verify tất ca萍 id thuộc user trước khi gán
    private async Task<List<Label>> ResolveLabelsAsync(Guid userId, List<Guid> labelIds)
    {
        var ids = labelIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var labels = await _labelRepository.GetByIdsAsync(userId, ids);

        if (labels.Count != ids.Count)
        {
            throw new ArgumentException("One or more labels not found");
        }

        return labels;
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
            Progress = CalculateProgress(project),
            Labels = project.Labels
                .Select(l => new LabelDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Color = l.Color
                })
                .ToList()
        };
    }
}