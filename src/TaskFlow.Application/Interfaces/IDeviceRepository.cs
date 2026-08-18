using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IDeviceRepository
{
    Task<UserDevice?> GetByFingerprintAsync(Guid userId, string fingerprint);
    Task<UserDevice?> GetByIdAsync(Guid id);
    Task<List<UserDevice>> GetActiveByUserIdAsync(Guid userId);
    Task<UserDevice> AddAsync(UserDevice device);
    Task<UserDevice> UpdateAsync(UserDevice device);
}
