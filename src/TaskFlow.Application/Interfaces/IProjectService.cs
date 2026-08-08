using TaskFlow.Application.DTOs.Projects;

namespace TaskFlow.Application.Interfaces;

public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync(
        Guid userId
    );

    Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        Guid userId
    );

    Task<ProjectResponse> CreateAsync(
        CreateOrUpdateProjectRequest request,
        Guid userId
    );

    Task<ProjectResponse> UpdateAsync(
        Guid projectId,
        CreateOrUpdateProjectRequest request,
        Guid userId
    );

    Task DeleteAsync(
        Guid projectId,
        Guid userId
    );
}