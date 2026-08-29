using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Notifications.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? TaskId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? MeetingId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
