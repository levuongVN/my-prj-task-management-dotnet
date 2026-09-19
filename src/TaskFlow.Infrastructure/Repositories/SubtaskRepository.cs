using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class SubtaskRepository(
    ApplicationDbContext context
) : ISubtaskRepository
{
    private readonly ApplicationDbContext _context =
        context;

    public async Task<List<SubtaskItem>> GetByTaskIdAsync(
        Guid taskId
    )
    {
        return await _context.Subtasks
            .Where(x =>
                x.TaskId == taskId &&
                x.IsDeleted == false
            )
            .OrderBy(x => x.Position)
            .ToListAsync();
    }

    public async Task<SubtaskItem?> GetByIdAsync(
        Guid id
    )
    {
        return await _context.Subtasks
            .Include(x => x.Task)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.IsDeleted == false
            );
    }

    public async Task AddAsync(
        SubtaskItem subtask
    )
    {
        await _context.Subtasks.AddAsync(subtask);
    }

    public void Update(
        SubtaskItem subtask
    )
    {
        _context.Subtasks.Update(subtask);
    }

    public void Delete(
        SubtaskItem subtask
    )
    {
        subtask.IsDeleted = true;
        subtask.UpdatedAt = DateTime.UtcNow;

        _context.Subtasks.Update(subtask);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
