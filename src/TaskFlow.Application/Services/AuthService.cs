using System.Threading.Tasks;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Features.Auth.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Auth.Services;

public class AuthService(
    IJwtTokenGenerator jwtTokenGenerator,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDeviceService deviceService
) : IAuthService
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IDeviceService _deviceService = deviceService;

    public async Task<AuthResponse> Login(
    LoginRequest request,
    string? ipAddress = null,
    string? deviceType = null,
    string? deviceName = null
)
    {
        var user = await _userRepository.GetByEmailAsync(
            request.Email
        );

        if (user == null)
        {
            throw new Exception("User not found");
        }

        var isValidPassword = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash
        );

        if (!isValidPassword)
        {
            throw new Exception("Invalid password");
        }

        UserDevice? device = null;

        if (request.Device != null)
        {
            device = await _deviceService.UpsertDeviceAsync(
                user.Id,
                request.Device,
                ipAddress,
                deviceType ?? "Unknown",
                deviceName ?? "Unknown device"
            );
        }

        var accessToken = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email,
            device?.Id ?? Guid.Empty
        );

        var refreshTokenString = GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            UserDeviceId = device?.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(
                refreshToken
            );

        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,

            RefreshToken = new RefreshTokenDto
            {
                Token = refreshToken.Token,
                ExpiresAt = refreshToken.ExpiresAt
            },

            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarPath,
                CreatedAt = user.CreatedAt
            }
        };
    }
    public async Task<AuthResponse> RefreshToken(RefreshTokenRequest request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken
        );

        if (storedToken == null)
        {
            throw new Exception("Refresh token not found");
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow || storedToken.IsRevoked)
        {
            throw new Exception("Refresh token is expired or revoked");
        }

        if (storedToken.UserDevice != null && !storedToken.UserDevice.IsActive)
        {
            throw new Exception("Device has been logged out");
        }

        if (storedToken.UserDevice != null)
        {
            storedToken.UserDevice.LastActiveAt = DateTime.UtcNow;
        }

        return new AuthResponse
        {
            AccessToken = _jwtTokenGenerator.GenerateToken(
                storedToken.UserId,
                storedToken.User.Email,
                storedToken.UserDeviceId ?? Guid.Empty
            ),
            RefreshToken = new RefreshTokenDto
            {
                Token = storedToken.Token,
                ExpiresAt = storedToken.ExpiresAt
            },
            User = new UserDto
            {
                Id = storedToken.User.Id,
                Email = storedToken.User.Email,
                FullName = storedToken.User.FullName,
                AvatarUrl = storedToken.User.AvatarPath,
                CreatedAt = storedToken.User.CreatedAt
            }
        };
    }

    private string GenerateRefreshToken()
    {
        return Guid.NewGuid().ToString() +
               Guid.NewGuid().ToString();
    }

}
