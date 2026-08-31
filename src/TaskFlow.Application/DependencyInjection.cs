using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Features.Auth.Services;
using TaskFlow.Application.Features.Tasks.Interfaces;
using TaskFlow.Application.Features.Tasks.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Application.Features.Meetings.Interfaces;
using TaskFlow.Application.Features.Meetings.Services;
using TaskFlow.Application.Features.Analytics.Interfaces;
using TaskFlow.Application.Features.Analytics.Services;

namespace TaskFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services
    )
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        return services;
    }
}