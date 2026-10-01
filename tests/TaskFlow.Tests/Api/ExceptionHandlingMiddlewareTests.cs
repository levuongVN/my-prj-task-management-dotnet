using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.API.MiddleWare;
using TaskFlow.Application.Common;

namespace TaskFlow.Tests.Api;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Body)> Run(Exception? thrown)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var nextCalled = false;

        var middleware = new ExceptionHandlingMiddleware(_ =>
        {
            if (thrown != null) throw thrown;
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, NullLogger<ExceptionHandlingMiddleware>.Instance);
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, nextCalled ? string.Empty : await reader.ReadToEndAsync());
    }

    private static int ExtractStatusCode(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("statusCodes").GetInt32();

    private static string ExtractMessage(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!;

    [Fact]
    public async Task KhongLoi_NextChayVaBodyRong()
    {
        var (status, body) = await Run(null);

        Assert.Equal(200, status);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public async Task NotFoundException_404()
    {
        var (_, body) = await Run(new NotFoundException("Project not found"));

        Assert.Equal(404, ExtractStatusCode(body));
        Assert.Equal("Project not found", ExtractMessage(body));
    }

    [Fact]
    public async Task KeyNotFoundException_404()
    {
        var (_, body) = await Run(new KeyNotFoundException("Task not found"));

        Assert.Equal(404, ExtractStatusCode(body));
    }

    [Fact]
    public async Task ConflictException_409()
    {
        var (_, body) = await Run(new ConflictException("Email already exists"));

        Assert.Equal(409, ExtractStatusCode(body));
        Assert.Equal("Email already exists", ExtractMessage(body));
    }

    [Fact]
    public async Task BadRequestException_400()
    {
        var (_, body) = await Run(new BadRequestException("bad"));

        Assert.Equal(400, ExtractStatusCode(body));
    }

    [Fact]
    public async Task ArgumentException_400()
    {
        var (_, body) = await Run(new ArgumentException("Title is required"));

        Assert.Equal(400, ExtractStatusCode(body));
        Assert.Equal("Title is required", ExtractMessage(body));
    }

    [Fact]
    public async Task UnauthorizedAccessException_401()
    {
        var (_, body) = await Run(new UnauthorizedAccessException("Invalid email or password"));

        Assert.Equal(401, ExtractStatusCode(body));
    }

    [Fact]
    public async Task ExceptionKhac_500VaGiauThongDiepNoiBo()
    {
        var (_, body) = await Run(new InvalidOperationException("connection string leaked"));

        Assert.Equal(500, ExtractStatusCode(body));
        Assert.Equal("Something went wrong", ExtractMessage(body));
    }

    // Case SSE: response đã gửi byte đầu rồi mới lỗi -> middleware KHÔNG được set
    // status (Kestrel sẽ ném lỗi thứ 2 che mất lỗi gốc). Phải nuốt lỗi + log.
    [Fact]
    public async Task LoiSauKhiResponseDaBatDau_KhongDoiStatusVaKhongNem()
    {
        var context = new DefaultHttpContext();

        // Giả lập response đã "started": thay feature mặc định bằng 1 feature luôn
        // báo HasStarted = true (ASP.NET Core không cho set HasStarted trực tiếp).
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("stream failed"));

        var ex = await Record.ExceptionAsync(() =>
            middleware.InvokeAsync(context, NullLogger<ExceptionHandlingMiddleware>.Instance));

        Assert.Null(ex);
        Assert.True(context.Response.HasStarted);
        Assert.Equal(200, context.Response.StatusCode);
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
