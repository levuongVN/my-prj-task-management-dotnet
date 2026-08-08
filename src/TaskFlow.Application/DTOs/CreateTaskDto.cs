namespace TaskFlow.Application.Features.Tasks.DTOs;

using TaskFlow.Domain.Enums;

public class CreateOrUpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ProjectId { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? Deadline { get; set; }
}