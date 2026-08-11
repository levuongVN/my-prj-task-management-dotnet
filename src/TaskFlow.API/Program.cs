using TaskFlow.Application;
using TaskFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(
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
                .AllowAnyMethod();
        });
});

// gọi các builder của các layer ở đây sau đó mới build app ở dưới, nếu không sẽ bị lỗi khi chạy app do chưa đăng ký các service của layer đó

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// MIDDLEWARE
app.UseCors("AllowFrontend");
app.UseMiddleware<ExceptionHandingMiddleware>();
//auth
//logg
app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();


// MAP CONTROLLERS
app.MapControllers();

app.Run();
