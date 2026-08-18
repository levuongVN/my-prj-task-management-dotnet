using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> Login(LoginRequest request, string? ipAddress = null, string? deviceType = null, string? deviceName = null);
    Task<AuthResponse> RefreshToken(RefreshTokenRequest request);
}