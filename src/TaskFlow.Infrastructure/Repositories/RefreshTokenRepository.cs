namespace TaskFlow.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(
        ApplicationDbContext context
    )
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .Include(x => x.User)
            .Include(x => x.UserDevice)
            .FirstOrDefaultAsync(x => x.Token == token);
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken);
    }

    public async Task RevokeByDeviceAsync(Guid userId, Guid deviceId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.UserDeviceId == deviceId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> CountActiveDevicesAsync(Guid userId)
    {
        return await _context.UserDevices.CountAsync(d => d.UserId == userId && d.IsActive);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}