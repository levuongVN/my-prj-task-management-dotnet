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
        int pageSize,
        Guid? labelId = null
    );
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize
    );

    // Position lớn nhất của task sống trong cùng scope (project nếu có, không thì
    // task cá nhân) - dùng để sinh recurring task ở DUỐI bảng/kanban
    Task<int> GetMaxPositionAsync(Guid userId, Guid? projectId);
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
