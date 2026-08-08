namespace TaskFlow.Application.Features.Tasks.DTOs;

public class TaskResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Status { get; set; }

    public int Priority { get; set; }

    public DateTime? Deadline { get; set; }

    public int Position { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}