using TaskFlow.Application;
using TaskFlow.Infrastructure;
using TaskFlow.API.MiddleWare;
using TaskFlow.API;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDetection();
builder.Services.AddApplication(); // DI of service
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddAPI(); // DI of controller
builder.Services.AddInfrastructure( // DI of infrastructure
    builder.Configuration
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// gọi các builder của các layer ở đây sau đó mới build app ở dưới, nếu không sẽ bị lỗi khi chạy app do chưa đăng ký các service của layer đó

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// MIDDLEWARE
app.UseDetection();
app.UseCors("AllowFrontend");
app.UseMiddleware<ExceptionHandingMiddleware>();
//auth
//logg
app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseMiddleware<DeviceTrackingMiddleware>();


// MAP CONTROLLERS
app.MapControllers();
app.MapHub<TaskFlow.API.Hubs.NotificationHub>("/hubs/notification");

app.Run();
