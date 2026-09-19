using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
    Task AddAsync(PasswordResetToken token);
    Task InvalidateUserTokensAsync(Guid userId);
    Task SaveChangesAsync();
}
