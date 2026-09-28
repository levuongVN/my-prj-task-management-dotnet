using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class TaskItem : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public Guid? ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public DateTime? Deadline { get; set; }

    public int Position { get; set; }

    // None = task thường. Có giá trị -> khi task được hoàn thành (status nhảy sang
    // Done) BE tự sinh task kế của chuỗi (clone + deadline đẩy sang kỳ tiếp)
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public Guid UserId { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation Property
    public User User { get; set; } = null!;

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<SubtaskItem> Subtasks { get; set; } = new List<SubtaskItem>();

    public ICollection<Label> Labels { get; set; } = new List<Label>();

}
