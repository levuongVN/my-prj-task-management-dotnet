using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Meeting : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public DateTime StartAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ProjectId { get; set; }

    public Project? Project { get; set; }
}
