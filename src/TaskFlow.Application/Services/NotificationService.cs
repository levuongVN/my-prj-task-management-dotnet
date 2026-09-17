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
        // Ủy thẳng xuống repository (bulk update) - service chỉ giữ nghiệp vụ,
        // không tự nạp từng notification rồi Update (đó là nguyên nhân bug bỏ sót
        // các notification cũ hơn 50 bản ghi trước đây)
        return await _notificationRepository.MarkAllAsReadAsync(userId);
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
        // Fast path: đã tồn tại -> trả luôn, không spam lại
        if (!string.IsNullOrEmpty(deduplicationKey))
        {
            var existing = await _notificationRepository.GetByDeduplicationKeyAsync(deduplicationKey);

            if (existing != null)
            {
                return ToDto(existing);
            }
        }

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            TaskId = taskId,
            ProjectId = projectId,
            MeetingId = meetingId,
            DeduplicationKey = deduplicationKey ?? string.Empty
        };

        // Repository tự xử lý race: nếu unique index trên DeduplicationKey bắt
        // được trùng (2 job chạy đồng thời), AddAsync trả về BẢN GHI ĐÃ TỒN TẠI
        // (reference khác object mình truyền vào) thay vì ném exception.
        var created = await _notificationRepository.AddAsync(notification);

        // Chỉ push realtime khi THẬT SỰ tạo mới (thắng race). Nếu thua race,
        // bản ghi đã được gửi realtime bởi job kia rồi -> không gửi trùng toast.
        if (ReferenceEquals(created, notification))
        {
            await _notificationSender.SendNotificationAsync(userId, created);
        }

        return ToDto(created);
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
