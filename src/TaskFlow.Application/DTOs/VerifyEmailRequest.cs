namespace TaskFlow.Application.Features.Auth.DTOs;

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}
