using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IAiChatMessageRepository
{
    Task<AiChatMessage> AddAsync(AiChatMessage message);

    Task<List<AiChatMessage>> GetPagedBySessionAsync(Guid sessionId, int page, int pageSize);

    // Lấy N tin nhắn gần nhất (đã sắp theo thời gian tăng dần) để làm context hội thoại
    Task<List<AiChatMessage>> GetLastBySessionAsync(Guid sessionId, int take);

    Task<int> CountBySessionAsync(Guid sessionId);

    // Đếm số tin nhắn user đã gửi trong ngày để enforce quota free tier
    Task<int> CountTodayByUserAsync(Guid userId);
}
