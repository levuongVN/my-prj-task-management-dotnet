using TaskFlow.Application.Features.Tasks.DTOs;

namespace TaskFlow.Application.Features.Tasks.Interfaces;

public interface ISubtaskService
{
    Task<List<SubtaskResponse>> GetByTaskAsync(Guid taskId, Guid userId);

    Task<SubtaskResponse> GetByIdAsync(Guid subtaskId, Guid userId);

    Task<SubtaskResponse> CreateAsync(CreateSubtaskRequest request, Guid userId);

    Task<SubtaskResponse> UpdateAsync(Guid subtaskId, Guid userId, UpdateSubtaskRequest request);

    Task<SubtaskResponse> ToggleAsync(Guid subtaskId, Guid userId);

    Task DeleteAsync(Guid subtaskId, Guid userId);

    Task<List<SubtaskResponse>> ReorderAsync(ReorderSubtasksRequest request, Guid userId);
}
