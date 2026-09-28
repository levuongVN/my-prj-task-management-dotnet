using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class MeetingRepository(
    ApplicationDbContext context
) : IMeetingRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<Meeting>> GetAllByUserIdAsync(Guid userId)
    {
        return await _context.Meetings
            .Where(x => x.UserId == userId)
            .Include(x => x.Project)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();
    }

    public async Task<(List<Meeting> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        int page,
        int pageSize
    )
    {
        var query = _context.Meetings
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(x => x.Project)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Meeting?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _context.Meetings
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId
            );
    }

    public async Task AddAsync(Meeting meeting)
    {
        await _context.Meetings.AddAsync(meeting);
    }

    public void Delete(Meeting meeting)
    {
        _context.Meetings.Remove(meeting);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}