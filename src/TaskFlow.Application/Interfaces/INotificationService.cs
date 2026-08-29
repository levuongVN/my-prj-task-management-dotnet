using TaskFlow.Application.Features.Notifications.DTOs;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetByUserIdAsync(Guid userId, int take = 50);
    Task<int> CountUnreadAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<int> MarkAllAsReadAsync(Guid userId);
    Task<NotificationDto> CreateAndSendAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? taskId = null,
        Guid? projectId = null,
        Guid? meetingId = null,
        string? deduplicationKey = null);
}
