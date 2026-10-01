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
using TaskFlow.Infrastructure.Storage;
using TaskFlow.Infrastructure.Email;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common;
using TaskFlow.Infrastructure.AI;

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
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<ISubtaskRepository, SubtaskRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<IFileStorageService, SupabaseStorageService>();

        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // OAuth providers: Google validate ID token, GitHub dùng typed HttpClient
        services.AddHttpClient<IGitHubAuthProvider, GitHubAuthProvider>();
        services.AddScoped<IGoogleAuthProvider, GoogleAuthProvider>();

        // ===== AI (Gemini free tier) =====
        // services.Configure<AiOptions>(...) = bind section "Ai" trong appsettings.json
        // vào class AiOptions (Options pattern). AiService inject IOptions<AiOptions>
        // để đọc ApiKey/Model/DailyUserLimit/RetentionDays.
        services.Configure<AiOptions>(configuration.GetSection("Ai"));

        // Đăng ký IChatClient - đây là "cầu nối" duy nhất giữa Application và SDK Gemini:
        //
        //   Application  ->  chỉ biết IChatClient (Microsoft.Extensions.AI.Abstractions)
        //   Infrastructure -> biết Google.GenAI (SDK chính thức của Google)
        //
        // Google.GenAI.Client là client gọi Gemini; extension AsIChatClient(model)
        // (trong namespace Microsoft.Extensions.AI) bọc nó lại thành IChatClient.
        // Ta bọc thêm 1 lớp ResilientChatClient để tự chuyển sang model dự phòng khi
        // model chính gặp 503/429 (free tier rất hay bị). Application vẫn chỉ thấy IChatClient.
        //
        // AddSingleton vì client này thread-safe và dùng chung được cho mọi request.
        // Factory chạy lazy (chỉ khi có request đầu tiên resolve) nên app vẫn khởi
        // động bình thường dù chưa cấu hình API key.
        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var apiKey = configuration["Ai:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(
                    "Ai:ApiKey is not configured. Get a free key at https://aistudio.google.com/apikey");

            // Thứ tự thử: model chính -> các model dự phòng (bỏ trùng, giữ thứ tự).
            var modelNames = new List<string>
            {
                configuration["Ai:Model"] ?? "gemini-flash-latest"
            };

            foreach (var fallback in configuration.GetSection("Ai:FallbackModels").GetChildren())
            {
                var name = fallback.Value;

                if (!string.IsNullOrWhiteSpace(name) &&
                    !modelNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    modelNames.Add(name);
                }
            }

            // 1 Client Gemini dùng chung; AsIChatClient(model) tạo wrapper cho từng model.
            var googleClient = new Google.GenAI.Client(apiKey: apiKey);
            var models = modelNames
                .Select(model => (Model: model, Client: googleClient.AsIChatClient(model)))
                .ToList();

            var logger = serviceProvider.GetRequiredService<ILogger<ResilientChatClient>>();
            return new ResilientChatClient(models, logger);
        });

        services.AddScoped<IAiChatSessionRepository, AiChatSessionRepository>();
        services.AddScoped<IAiChatMessageRepository, AiChatMessageRepository>();
        services.AddScoped<AiChatRetentionJob>();

        return services;
    }
}