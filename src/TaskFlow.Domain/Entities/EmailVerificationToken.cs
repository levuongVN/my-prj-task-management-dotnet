using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class EmailVerificationToken : BaseEntity
{
    // SHA-256 hex của raw token trong link email - DB không giữ token thô
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    // Navigation Property
    public User User { get; set; } = null!;
}
