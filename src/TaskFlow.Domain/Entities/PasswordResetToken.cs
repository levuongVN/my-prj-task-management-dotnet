using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class PasswordResetToken : BaseEntity
{
    // SHA-256 hex của raw token. Raw token chỉ tồn tại trong link gửi qua email,
    // DB chỉ giữ hash để raw token bị lộ DB cũng không dùng được.
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    // Navigation Property
    public User User { get; set; } = null!;
}
