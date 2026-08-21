using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IDeviceService
{
    Task<UserDevice> UpsertDeviceAsync(Guid userId, DeviceRequest deviceRequest, string? ipAddress, string deviceType, string deviceName);
    Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId, Guid currentDeviceId);
    Task RevokeDeviceAsync(Guid userId, Guid deviceId);
}
