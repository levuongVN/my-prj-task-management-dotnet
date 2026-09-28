using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> GetByTokenHashAsync(string tokenHash);
    Task AddAsync(EmailVerificationToken token);
    Task InvalidateUserTokensAsync(Guid userId);
    Task SaveChangesAsync();
}
