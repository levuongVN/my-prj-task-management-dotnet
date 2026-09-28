using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Tasks.DTOs;

namespace TaskFlow.Application.Features.Tasks.Interfaces;

public interface ITaskService
{
    Task<List<TaskResponse>>
    GetAllByUserAsync(
        Guid userId
    );

    Task<PagedResult<TaskResponse>> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize
    );

    Task<PagedResult<TaskResponse>> GetPagedProjectTasksAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize
    );

    Task<List<TaskResponse>> GetProjectTasksAsync(
        Guid projectId,
        Guid userId
    );

    Task<TaskResponse> GetByIdAsync(
        Guid taskId,
        Guid userId
    );

    Task<TaskResponse> CreateAsync(
        CreateOrUpdateTaskRequest request,
        Guid userId
    );

    Task<TaskResponse> UpdateAsync(
        Guid taskId,
        Guid userId,
        CreateOrUpdateTaskRequest request
    );

    Task DeleteAsync(
        Guid taskId,
        Guid userId
    );
}