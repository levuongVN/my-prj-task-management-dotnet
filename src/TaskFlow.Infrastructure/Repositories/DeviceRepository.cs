using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public DeviceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDevice?> GetByFingerprintAsync(Guid userId, string fingerprint)
    {
        return await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceFingerprint == fingerprint);
    }

    public async Task<UserDevice?> GetByIdAsync(Guid id)
    {
        return await _context.UserDevices.FindAsync(id);
    }

    public async Task<List<UserDevice>> GetActiveByUserIdAsync(Guid userId)
    {
        return await _context.UserDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.LastActiveAt)
            .ToListAsync();
    }

    public async Task<UserDevice> AddAsync(UserDevice device)
    {
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<UserDevice> UpdateAsync(UserDevice device)
    {
        _context.UserDevices.Update(device);
        await _context.SaveChangesAsync();
        return device;
    }
}
