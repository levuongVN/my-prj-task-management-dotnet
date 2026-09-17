namespace TaskFlow.Application.Features.Auth.DTOs;

public class GithubLoginRequest
{
    // Authorization code GitHub redirect về FE callback
    public string Code { get; set; } = string.Empty;

    public DeviceRequest? Device { get; set; }
}
