using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class SubtaskItem : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; } = false;

    public int Position { get; set; }

    public Guid TaskId { get; set; }

    public TaskItem Task { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
}
