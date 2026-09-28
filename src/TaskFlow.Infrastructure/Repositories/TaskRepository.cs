using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class TaskRepository(
    ApplicationDbContext context
) : ITaskRepository
{
    private readonly ApplicationDbContext _context =
        context;

    public async Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid userId
    )
    {
        return await _context.Tasks
            .Include(x => x.Subtasks)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId && x.IsDeleted == false
            );
    }

    public async Task<List<TaskItem>>
        GetByProjectIdAsync(
            Guid projectId,
            Guid userId
        )
    {
        return await _context.Tasks
            .Where(x =>
                x.ProjectId == projectId &&
                x.UserId == userId && x.IsDeleted == false
            )
            .Include(x => x.Subtasks)
            .OrderBy(x => x.Position)
            .ToListAsync();
    }

    public async Task AddAsync(
        TaskItem task
    )
    {
        await _context.Tasks.AddAsync(task);
    }

    public void Update(
        TaskItem task
    )
    {
        _context.Tasks.Update(task);
    }

    public void Delete(
        TaskItem task
    )
    {
        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<List<TaskItem>> GetAllByUserIdAsync(Guid userId)
    {
        return await _context.Tasks
            .Where(x => x.UserId == userId && x.IsDeleted == false)
            .Include(x => x.Project)
            .Include(x => x.Subtasks)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    // Count + Skip/Take: 1 query đếm + 1 query lấy trang hiện tại,
    // KHÔNG Include ở query Count để nhẹ
    public async Task<(List<TaskItem> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        int page,
        int pageSize
    )
    {
        var query = _context.Tasks
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(x => x.Project)
            .Include(x => x.Subtasks)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // Task trong project sắp theo Position (thứ tự kỳ vọng của FE kanban/board),
    // khác list "all tasks" sắp theo CreatedAt
    public async Task<(List<TaskItem> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize
    )
    {
        var query = _context.Tasks
            .Where(x =>
                x.ProjectId == projectId &&
                x.UserId == userId &&
                !x.IsDeleted
            )
            .OrderBy(x => x.Position);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(x => x.Subtasks)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
