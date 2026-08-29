using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface INotificationRepository
{
    Task<List<Notification>> GetByUserIdAsync(Guid userId, int take = 50);
    Task<int> CountUnreadAsync(Guid userId);
    Task<Notification?> GetByIdAsync(Guid id);
    Task<Notification> AddAsync(Notification notification);
    Task<Notification?> GetByDeduplicationKeyAsync(string deduplicationKey);
    Task<Notification> UpdateAsync(Notification notification);
}
