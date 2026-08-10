using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public Guid? TaskId { get; set; }
    public TaskItem? Task { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public DateTime? ReadAt { get; set; }

    public string DeduplicationKey { get; set; } = string.Empty;
}
