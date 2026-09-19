namespace TaskFlow.Application.Features.Tasks.DTOs;

public class SubtaskResponse
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class CreateSubtaskRequest
{
    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;
}

public class UpdateSubtaskRequest
{
    public string Title { get; set; } = string.Empty;
}

public class ReorderSubtasksRequest
{
    public Guid TaskId { get; set; }

    public List<Guid> OrderedSubtaskIds { get; set; } = new();
}
