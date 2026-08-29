using TaskFlow.Application.Features.Notifications.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationSender _notificationSender;

    public NotificationService(
        INotificationRepository notificationRepository,
        INotificationSender notificationSender)
    {
        _notificationRepository = notificationRepository;
        _notificationSender = notificationSender;
    }

    public async Task<List<NotificationDto>> GetByUserIdAsync(Guid userId, int take = 50)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId, take);

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            TaskId = n.TaskId,
            ProjectId = n.ProjectId,
            MeetingId = n.MeetingId,
            IsRead = n.ReadAt != null,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<int> CountUnreadAsync(Guid userId)
    {
        return await _notificationRepository.CountUnreadAsync(userId);
    }

    public async Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);

        if (notification == null || notification.UserId != userId)
        {
            return false;
        }

        notification.ReadAt = DateTime.UtcNow;
        await _notificationRepository.UpdateAsync(notification);

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId);

        var unread = notifications.Where(n => n.ReadAt == null).ToList();

        if (unread.Count == 0)
        {
            return 0;
        }

        foreach (var notification in unread)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _notificationRepository.UpdateAsync(notification);
        }

        return unread.Count;
    }

    public async Task<NotificationDto> CreateAndSendAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? taskId = null,
        Guid? projectId = null,
        Guid? meetingId = null,
        string? deduplicationKey = null)
    {
        if (!string.IsNullOrEmpty(deduplicationKey))
        {
            var existing = await _notificationRepository.GetByDeduplicationKeyAsync(deduplicationKey);

            if (existing != null)
            {
                return ToDto(existing);
            }
        }

        var notification = await _notificationRepository.AddAsync(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            TaskId = taskId,
            ProjectId = projectId,
            MeetingId = meetingId,
            DeduplicationKey = deduplicationKey ?? string.Empty
        });

        await _notificationSender.SendNotificationAsync(userId, notification);

        return ToDto(notification);
    }

    private static NotificationDto ToDto(Notification n)
    {
        return new NotificationDto
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            TaskId = n.TaskId,
            ProjectId = n.ProjectId,
            MeetingId = n.MeetingId,
            IsRead = n.ReadAt != null,
            CreatedAt = n.CreatedAt
        };
    }
}
