using NSubstitute;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Devices;

public class DeviceServiceTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();

    private DeviceService Sut() => new(_devices, _refreshTokens);

    private static readonly Guid UserId = Guid.NewGuid();

    private static DeviceRequest Req(string fingerprint = "fp-new", string? pushToken = null) =>
        new() { Fingerprint = fingerprint, PushToken = pushToken };

    private static UserDevice Device(string fingerprint, DateTime lastLogin) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        DeviceFingerprint = fingerprint,
        DeviceType = "Web",
        LastLoginAt = lastLogin,
        IsActive = true,
    };

    [Fact]
    public async Task Upsert_FingerprintDaCo_UpdateThongTinVaActive()
    {
        var existing = Device("fp-old", DateTime.UtcNow.AddDays(-1));
        _devices.GetByFingerprintAsync(UserId, "fp-old").Returns(existing);
        _devices.UpdateAsync(Arg.Any<UserDevice>()).Returns(ci => ci.Arg<UserDevice>());

        var res = await Sut().UpsertDeviceAsync(UserId, Req("fp-old", pushToken: "push-1"), "1.2.3.4", "Mobile", "iPhone");

        Assert.Equal("Mobile", res.DeviceType);
        Assert.Equal("iPhone", res.DeviceName);
        Assert.Equal("1.2.3.4", res.IpAddress);
        Assert.Equal("push-1", res.DeviceToken);
        Assert.True(res.IsActive);
        Assert.True(res.LastLoginAt > DateTime.UtcNow.AddMinutes(-1));
        await _devices.Received(1).UpdateAsync(existing);
        await _devices.DidNotReceive().AddAsync(Arg.Any<UserDevice>());
    }

    [Fact]
    public async Task Upsert_PushTokenRong_GiuTokenCu()
    {
        var existing = Device("fp-old", DateTime.UtcNow.AddDays(-1));
        existing.DeviceToken = "keep-me";
        _devices.GetByFingerprintAsync(UserId, "fp-old").Returns(existing);
        _devices.UpdateAsync(Arg.Any<UserDevice>()).Returns(ci => ci.Arg<UserDevice>());

        await Sut().UpsertDeviceAsync(UserId, Req("fp-old"), null!, "Web", "Chrome");

        Assert.Equal("keep-me", existing.DeviceToken);
    }

    [Fact]
    public async Task Upsert_FingerprintMoi_Du3Active_ChiemChoDeviceCuNhat()
    {
        var oldest = Device("fp-oldest", DateTime.UtcNow.AddDays(-10));
        var mid = Device("fp-mid", DateTime.UtcNow.AddDays(-5));
        var newest = Device("fp-newest", DateTime.UtcNow.AddDays(-1));
        _devices.GetByFingerprintAsync(UserId, "fp-incoming").Returns((UserDevice?)null);
        _devices.GetActiveByUserIdAsync(UserId).Returns([oldest, mid, newest]);
        _devices.UpdateAsync(Arg.Any<UserDevice>()).Returns(ci => ci.Arg<UserDevice>());

        var res = await Sut().UpsertDeviceAsync(UserId, Req("fp-incoming"), "1.1.1.1", "Web", "Safari");

        await _refreshTokens.Received(1).RevokeByDeviceAsync(UserId, oldest.Id);
        Assert.Equal("fp-incoming", res.DeviceFingerprint);
        Assert.Equal(oldest.Id, res.Id);
        Assert.True(res.IsActive);
        await _devices.DidNotReceive().AddAsync(Arg.Any<UserDevice>());
    }

    [Fact]
    public async Task Upsert_FingerprintMoi_Duoi3Active_TaoDeviceMoi()
    {
        _devices.GetByFingerprintAsync(UserId, "fp-brand-new").Returns((UserDevice?)null);
        _devices.GetActiveByUserIdAsync(UserId).Returns([Device("fp-1", DateTime.UtcNow)]);
        _devices.AddAsync(Arg.Any<UserDevice>()).Returns(ci => ci.Arg<UserDevice>());

        var res = await Sut().UpsertDeviceAsync(UserId, Req("fp-brand-new"), "2.2.2.2", "Desktop", "Mac");

        Assert.Equal("fp-brand-new", res.DeviceFingerprint);
        Assert.Equal("Mac", res.DeviceName);
        await _refreshTokens.DidNotReceive().RevokeByDeviceAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await _devices.Received(1).AddAsync(Arg.Any<UserDevice>());
    }

    [Fact]
    public async Task GetUserDevices_DanhDauDungCurrentDevice()
    {
        var current = Device("fp-a", DateTime.UtcNow);
        var other = Device("fp-b", DateTime.UtcNow);
        _devices.GetActiveByUserIdAsync(UserId).Returns([current, other]);

        var res = await Sut().GetUserDevicesAsync(UserId, current.Id);

        Assert.Equal(2, res.Count);
        Assert.True(res.Single(d => d.Id == current.Id).IsCurrentDevice);
        Assert.False(res.Single(d => d.Id == other.Id).IsCurrentDevice);
    }

    [Fact]
    public async Task Revoke_KhongTonTai_NemKeyNotFound()
    {
        _devices.GetByIdAsync(Arg.Any<Guid>()).Returns((UserDevice?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().RevokeDeviceAsync(UserId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Revoke_DeviceCuaUserKhac_NemKeyNotFound()
    {
        var device = Device("fp-x", DateTime.UtcNow);
        device.UserId = Guid.NewGuid();
        _devices.GetByIdAsync(device.Id).Returns(device);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().RevokeDeviceAsync(UserId, device.Id));
    }

    [Fact]
    public async Task Revoke_HopLe_RevokeRefreshTokenVaDeactivate()
    {
        var device = Device("fp-x", DateTime.UtcNow);
        _devices.GetByIdAsync(device.Id).Returns(device);
        _devices.UpdateAsync(Arg.Any<UserDevice>()).Returns(ci => ci.Arg<UserDevice>());

        await Sut().RevokeDeviceAsync(UserId, device.Id);

        Assert.False(device.IsActive);
        await _refreshTokens.Received(1).RevokeByDeviceAsync(UserId, device.Id);
        await _devices.Received(1).UpdateAsync(device);
    }
}
