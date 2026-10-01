using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class AiChatSessionRepository : IAiChatSessionRepository
{
    private readonly ApplicationDbContext _context;

    public AiChatSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AiChatSession?> GetByIdAsync(Guid id)
    {
        return await _context.AiChatSessions.FindAsync(id);
    }

    public async Task<AiChatSession> AddAsync(AiChatSession session)
    {
        _context.AiChatSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<(List<AiChatSession> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize
    )
    {
        var query = _context.AiChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task DeleteAsync(AiChatSession session)
    {
        _context.AiChatSessions.Remove(session);
        await _context.SaveChangesAsync();
    }

    // ExecuteUpdate sinh 1 câu UPDATE duy nhất (không load entity) - dùng khi có
    // tin nhắn mới để session nhảy lên đầu danh sách gần đây
    public async Task TouchAsync(Guid sessionId)
    {
        await _context.AiChatSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
    }
}
