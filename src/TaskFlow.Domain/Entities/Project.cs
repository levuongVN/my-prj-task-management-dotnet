using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Project : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ProjectStatus Status { get; set; }

    public DateTime Due { get; set; }
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;

    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Meeting> Meetings { get; set; } = [];
}
