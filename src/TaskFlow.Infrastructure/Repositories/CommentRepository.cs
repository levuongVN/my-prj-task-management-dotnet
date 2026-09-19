using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class CommentRepository(
    ApplicationDbContext context
) : ICommentRepository
{
    private readonly ApplicationDbContext _context =
        context;

    public async Task<List<Comment>> GetByTaskIdAsync(
        Guid taskId
    )
    {
        return await _context.Comments
            .Where(x =>
                x.TaskId == taskId &&
                x.IsDeleted == false
            )
            .Include(x => x.Author)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment?> GetByIdAsync(
        Guid id,
        Guid userId
    )
    {
        return await _context.Comments
            .Include(x => x.Author)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.AuthorId == userId &&
                x.IsDeleted == false
            );
    }

    public async Task AddAsync(
        Comment comment
    )
    {
        await _context.Comments.AddAsync(comment);
    }

    public void Update(
        Comment comment
    )
    {
        _context.Comments.Update(comment);
    }

    public void Delete(
        Comment comment
    )
    {
        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        _context.Comments.Update(comment);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
