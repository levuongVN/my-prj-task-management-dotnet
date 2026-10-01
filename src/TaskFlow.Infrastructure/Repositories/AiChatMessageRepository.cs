using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class AiChatMessageRepository : IAiChatMessageRepository
{
    private readonly ApplicationDbContext _context;

    public AiChatMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AiChatMessage> AddAsync(AiChatMessage message)
    {
        _context.AiChatMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<List<AiChatMessage>> GetPagedBySessionAsync(Guid sessionId, int page, int pageSize)
    {
        return await _context.AiChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<AiChatMessage>> GetLastBySessionAsync(Guid sessionId, int take)
    {
        // Lấy N tin mới nhất (DESC) rồi Reverse -> trả về đúng thứ tự thời gian
        // tăng dần (cũ -> mới) mà LLM API yêu cầu cho lịch sử hội thoại
        var messages = await _context.AiChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();

        messages.Reverse();
        return messages;
    }

    public async Task<int> CountBySessionAsync(Guid sessionId)
    {
        return await _context.AiChatMessages
            .CountAsync(m => m.SessionId == sessionId);
    }

    public async Task<int> CountTodayByUserAsync(Guid userId)
    {
        var todayStart = DateTime.UtcNow.Date;

        // Join sang Session để lọc theo user (message không giữ UserId trực tiếp)
        // và đếm cả tin của user lẫn của AI trong ngày (mỗi lượt chat tốn 1 call AI)
        return await _context.AiChatMessages
            .CountAsync(m => m.Session!.UserId == userId && m.CreatedAt >= todayStart);
    }
}
