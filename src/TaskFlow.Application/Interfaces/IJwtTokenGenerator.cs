namespace TaskFlow.Application.Features.Auth.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(
        Guid userId,
        string email
    );
}