using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Features.Auth.Services;
using TaskFlow.Application.Features.Tasks.Interfaces;
using TaskFlow.Application.Features.Tasks.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;

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
        return services;
    }
}