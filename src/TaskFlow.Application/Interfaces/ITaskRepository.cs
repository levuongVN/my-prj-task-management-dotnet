using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface ITaskRepository
{
    Task<List<TaskItem>> GetAllByUserIdAsync(
    Guid userId
);
    Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid userId
    );

    Task<List<TaskItem>> GetByProjectIdAsync(
        Guid projectId,
        Guid userId
    );

    Task AddAsync(TaskItem task);

    Task UpdateAsync(TaskItem task);

    Task DeleteAsync(TaskItem task);

    Task SaveChangesAsync();
}