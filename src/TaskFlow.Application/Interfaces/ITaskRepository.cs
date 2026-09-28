using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface ITaskRepository
{
    Task<List<TaskItem>> GetAllByUserIdAsync(
    Guid userId
);
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        int page,
        int pageSize
    );
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize
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

    void Update(TaskItem task);

    void Delete(TaskItem task);

    Task SaveChangesAsync();
}
