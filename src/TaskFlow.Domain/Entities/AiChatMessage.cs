using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class AiChatMessage : AuditableEntity
{
    public Guid SessionId { get; set; }
    public AiChatSession Session { get; set; } = null!;

    public AiChatRole Role { get; set; }

    public string Content { get; set; } = string.Empty;
}
