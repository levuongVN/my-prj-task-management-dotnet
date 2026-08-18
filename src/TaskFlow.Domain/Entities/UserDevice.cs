using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class UserDevice : AuditableEntity
{
    public Guid UserId { get; set; }

    public string DeviceFingerprint { get; set; } = string.Empty;

    public string? DeviceToken { get; set; }

    public string DeviceType { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Property
    public User User { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}