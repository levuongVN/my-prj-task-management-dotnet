using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    private const int MaxActiveDevices = 3;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _deviceRepository = deviceRepository;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<UserDevice> UpsertDeviceAsync(
        Guid userId,
        DeviceRequest deviceRequest,
        string? ipAddress,
        string deviceType,
        string deviceName)
    {
        var existingDevice = await _deviceRepository.GetByFingerprintAsync(userId, deviceRequest.Fingerprint);

        if (existingDevice != null)
        {
            existingDevice.DeviceType = deviceType;
            existingDevice.DeviceName = deviceName;
            existingDevice.IpAddress = ipAddress;
            existingDevice.LastActiveAt = DateTime.UtcNow;
            existingDevice.LastLoginAt = DateTime.UtcNow;
            existingDevice.IsActive = true;

            if (!string.IsNullOrEmpty(deviceRequest.PushToken))
            {
                existingDevice.DeviceToken = deviceRequest.PushToken;
            }

            return await _deviceRepository.UpdateAsync(existingDevice);
        }

        var activeDevices = await _deviceRepository.GetActiveByUserIdAsync(userId);

        if (activeDevices.Count >= MaxActiveDevices)
        {
            var oldestDevice = activeDevices.First();

            await _refreshTokenRepository.RevokeByDeviceAsync(userId, oldestDevice.Id);

            oldestDevice.DeviceFingerprint = deviceRequest.Fingerprint;
            oldestDevice.DeviceType = deviceType;
            oldestDevice.DeviceName = deviceName;
            oldestDevice.DeviceToken = deviceRequest.PushToken;
            oldestDevice.IpAddress = ipAddress;
            oldestDevice.LastActiveAt = DateTime.UtcNow;
            oldestDevice.LastLoginAt = DateTime.UtcNow;
            oldestDevice.IsActive = true;

            return await _deviceRepository.UpdateAsync(oldestDevice);
        }

        var newDevice = new UserDevice
        {
            UserId = userId,
            DeviceFingerprint = deviceRequest.Fingerprint,
            DeviceType = deviceType,
            DeviceName = deviceName,
            DeviceToken = deviceRequest.PushToken,
            IpAddress = ipAddress,
            LastActiveAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _deviceRepository.AddAsync(newDevice);
    }

    public async Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId, Guid currentDeviceId)
    {
        var devices = await _deviceRepository.GetActiveByUserIdAsync(userId);

        return devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            DeviceType = d.DeviceType,
            DeviceName = d.DeviceName,
            IpAddress = d.IpAddress,
            LastActiveAt = d.LastActiveAt,
            LastLoginAt = d.LastLoginAt,
            IsActive = d.IsActive,
            IsCurrentDevice = d.Id == currentDeviceId
        }).ToList();
    }

    public async Task RevokeDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);

        if (device == null || device.UserId != userId)
        {
            throw new KeyNotFoundException("Device not found");
        }

        await _refreshTokenRepository.RevokeByDeviceAsync(userId, deviceId);

        device.IsActive = false;
        await _deviceRepository.UpdateAsync(device);
    }
}
