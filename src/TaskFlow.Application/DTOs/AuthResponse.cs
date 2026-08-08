namespace TaskFlow.Application.Features.Auth.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public UserDto? User { get; set; } = null!;
    public RefreshTokenDto RefreshToken { get; set; } = null!;
}