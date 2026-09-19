using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Comment : AuditableEntity
{
    public string Content { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public TaskItem Task { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public bool IsDeleted { get; set; } = false;
}
