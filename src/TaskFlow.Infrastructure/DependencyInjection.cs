using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Application.Features.Auth.Interfaces;
using TaskFlow.Infrastructure.Auth;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Amazon.S3;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.PostgreSql;
using TaskFlow.Infrastructure.Jobs;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")
            )
        );

        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(
                    configuration.GetConnectionString("DefaultConnection")
                )
            )
        );
        services.AddHangfireServer(); // This is "Worker" it will process the jobs in the background
        services.AddScoped<NotificationJobs>();
        services.AddHostedService<NotificationJobScheduler>(); // This is "Scheduler" it will schedule the jobs to be processed by the worker

        services.AddAuthentication(
            JwtBearerDefaults.AuthenticationScheme
        )
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                configuration["Jwt:Secret"]!
                            )
                        )
                };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notification"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage")
        );
        
        services.AddSingleton<IAmazonS3>(serviceProvider =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<SupabaseStorageOptions>>().Value;

                    var credentials = new BasicAWSCredentials(
                        options.AccessKeyId,
                        options.SecretAccessKey
                    );

                    var s3Config = new AmazonS3Config
                    {
                        ServiceURL = options.Endpoint,

                        ForcePathStyle = true,

                        AuthenticationRegion = options.Region
                    };

                    return new AmazonS3Client(
                        credentials,
                        s3Config
                    );
                });


        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IFileStorageService, SupabaseStorageService>();
        return services;
    }
}