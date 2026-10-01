using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IAiChatSessionRepository
{
    Task<AiChatSession?> GetByIdAsync(Guid id);

    Task<AiChatSession> AddAsync(AiChatSession session);

    Task<(List<AiChatSession> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize
    );

    Task DeleteAsync(AiChatSession session);

    // Bump UpdatedAt khi có tin nhắn mới (dùng ExecuteUpdate, không load entity)
    Task TouchAsync(Guid sessionId);
}
