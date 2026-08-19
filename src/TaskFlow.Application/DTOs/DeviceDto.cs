namespace TaskFlow.Application.Features.Auth.DTOs;

public class DeviceDto
{
    public Guid Id { get; set; }

    public string DeviceType { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public string? IpAddress { get; set; }

    public DateTime LastActiveAt { get; set; }

    public DateTime LastLoginAt { get; set; }

    public bool IsActive { get; set; }

    public bool IsCurrentDevice { get; set; }
}
