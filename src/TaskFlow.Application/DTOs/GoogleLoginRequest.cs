namespace TaskFlow.Application.Features.Auth.DTOs;

public class GoogleLoginRequest
{
    // ID token (JWT string) trả về từ Google Identity Services phía FE
    public string IdToken { get; set; } = string.Empty;

    public DeviceRequest? Device { get; set; }
}
