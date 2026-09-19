using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    public PasswordResetTokenRepository(
        ApplicationDbContext context
    )
    {
        _context = context;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
    }

    public async Task AddAsync(PasswordResetToken token)
    {
        await _context.PasswordResetTokens.AddAsync(token);
    }

    // Luôn đặt token mới nghĩa là token cũ không còn giá trị
    // (nhân viên IT gọi lại nhiều lần cũng vẫn chỉ token mới nhất trong email chạy được)
    public async Task InvalidateUserTokensAsync(Guid userId)
    {
        var tokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
