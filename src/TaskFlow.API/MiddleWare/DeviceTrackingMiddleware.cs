using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.API.MiddleWare;

public class DeviceTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<Guid, DateTime> _lastUpdateMap = new();
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMinutes(5);

    public DeviceTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var deviceIdClaim = context.User?.FindFirstValue("device_id");

        if (Guid.TryParse(deviceIdClaim, out var deviceId) && deviceId != Guid.Empty)
        {
            var now = DateTime.UtcNow;

            if (!_lastUpdateMap.TryGetValue(deviceId, out var lastUpdate) ||
                (now - lastUpdate) > ThrottleInterval)
            {
                _lastUpdateMap[deviceId] = now;

                var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var device = await db.UserDevices.FindAsync(deviceId);

                if (device != null && device.IsActive)
                {
                    device.LastActiveAt = now;
                    await db.SaveChangesAsync();
                }
            }
        }

        await _next(context);
    }
}
