using Microsoft.Extensions.DependencyInjection;
using TaskFlow.API.Services;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.API;

public static class DependencyInjection
{
    public static IServiceCollection AddAPI(
        this IServiceCollection services
    )
    {
        services.AddScoped<INotificationSender, NotificationSender>();
        return services;
    }
}
