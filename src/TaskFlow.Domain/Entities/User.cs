using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? AvatarPath { get; set; }

    // null = email chưa verify (register bằng password), có giá trị = đã verify
    // (click link trong email, hoặc OAuth - email đã được provider verify sẵn)
    public DateTime? EmailVerifiedAt { get; set; }

    // Navigation Properties
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<UserDevice> Devices { get; set; } = new List<UserDevice>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();
}
