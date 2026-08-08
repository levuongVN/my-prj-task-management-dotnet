using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public Guid? ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public DateTime? Deadline { get; set; }

    public int Position { get; set; }

    public Guid UserId { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation Property
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}