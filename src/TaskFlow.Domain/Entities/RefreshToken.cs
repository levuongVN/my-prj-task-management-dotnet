using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public Guid UserId { get; set; }

    public Guid? UserDeviceId { get; set; }

    // Navigation Property
    public User User { get; set; } = null!;
    public UserDevice? UserDevice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
