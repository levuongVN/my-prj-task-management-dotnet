using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.API.MiddleWare;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Tests.Api;

public class DeviceTrackingMiddlewareTests
{
    private static string NewDbName() => "device-tracking-" + Guid.NewGuid();

    private static ServiceProvider BuildProvider(string dbName) =>
        new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName))
            .BuildServiceProvider();

    private static DefaultHttpContext MakeContext(IServiceProvider services, Guid deviceId) =>
        new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("device_id", deviceId.ToString()),
            ])),
        };

    private static async Task SeedDeviceAsync(ServiceProvider provider, string dbName, Guid deviceId, bool isActive)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserDevices.Add(new UserDevice
        {
            Id = deviceId,
            UserId = Guid.NewGuid(),
            DeviceFingerprint = "fp",
            IsActive = isActive,
            LastActiveAt = DateTime.UtcNow.AddDays(-2),
        });
        await db.SaveChangesAsync();
    }

    private static async Task<UserDevice?> LoadDeviceAsync(ServiceProvider provider, string dbName, Guid deviceId)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
    }

    [Fact]
    public async Task KhongCoClaim_NextVanChayKhongLoi()
    {
        var provider = BuildProvider(NewDbName());
        var context = new DefaultHttpContext { RequestServices = provider };
        var nextRan = false;
        var middleware = new DeviceTrackingMiddleware(_ => { nextRan = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextRan);
    }

    [Fact]
    public async Task ClaimKhongPhaiGuid_NextVanChay()
    {
        var provider = BuildProvider(NewDbName());
        var context = new DefaultHttpContext { RequestServices = provider };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("device_id", "khong-phai-guid")]));
        var nextRan = false;
        var middleware = new DeviceTrackingMiddleware(_ => { nextRan = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextRan);
    }

    [Fact]
    public async Task DeviceActive_CapNhatLastActiveAt()
    {
        var dbName = NewDbName();
        var provider = BuildProvider(dbName);
        var deviceId = Guid.NewGuid();
        await SeedDeviceAsync(provider, dbName, deviceId, isActive: true);
        var middleware = new DeviceTrackingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(MakeContext(provider, deviceId));

        var device = await LoadDeviceAsync(provider, dbName, deviceId);
        Assert.True(device!.LastActiveAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task DeviceInactive_KhongCapNhat()
    {
        var dbName = NewDbName();
        var provider = BuildProvider(dbName);
        var deviceId = Guid.NewGuid();
        await SeedDeviceAsync(provider, dbName, deviceId, isActive: false);
        var before = (await LoadDeviceAsync(provider, dbName, deviceId))!.LastActiveAt;
        var middleware = new DeviceTrackingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(MakeContext(provider, deviceId));

        var device = await LoadDeviceAsync(provider, dbName, deviceId);
        Assert.Equal(before, device!.LastActiveAt);
    }

    [Fact]
    public async Task DeviceKhongTonTai_KhongLoiNextVanChay()
    {
        var provider = BuildProvider(NewDbName());
        var nextRan = false;
        var middleware = new DeviceTrackingMiddleware(_ => { nextRan = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(MakeContext(provider, Guid.NewGuid()));

        Assert.True(nextRan);
    }

    [Fact]
    public async Task RequestThu2Trong5Phut_ThrottleKhongCapNhatLai()
    {
        var dbName = NewDbName();
        var provider = BuildProvider(dbName);
        var deviceId = Guid.NewGuid();
        await SeedDeviceAsync(provider, dbName, deviceId, isActive: true);
        var middleware = new DeviceTrackingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(MakeContext(provider, deviceId));
        var afterFirst = (await LoadDeviceAsync(provider, dbName, deviceId))!.LastActiveAt;

        await middleware.InvokeAsync(MakeContext(provider, deviceId));
        var afterSecond = (await LoadDeviceAsync(provider, dbName, deviceId))!.LastActiveAt;

        Assert.Equal(afterFirst, afterSecond);
    }
}
