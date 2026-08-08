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
            .OrderBy(x => x.Position)
            .ToListAsync();
    }

    public async Task AddAsync(
        TaskItem task
    )
    {
        await _context.Tasks.AddAsync(task);
    }

    public Task UpdateAsync(
        TaskItem task
    )
    {
        _context.Tasks.Update(task);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        TaskItem task
    )
    {
        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);

        return Task.CompletedTask;
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
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}