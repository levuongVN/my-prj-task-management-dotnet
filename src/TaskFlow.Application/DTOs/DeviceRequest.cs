namespace TaskFlow.Application.Features.Auth.DTOs;

public class DeviceRequest
{
    public string Fingerprint { get; set; } = string.Empty;

    public string? PushToken { get; set; }
}
