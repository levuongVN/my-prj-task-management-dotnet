using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class AiChatSession : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public List<AiChatMessage> Messages { get; set; } = new();
}
