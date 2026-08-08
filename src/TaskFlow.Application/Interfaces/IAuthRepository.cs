using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);

    Task SaveChangesAsync();
}