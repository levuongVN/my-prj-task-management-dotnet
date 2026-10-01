using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common;

namespace TaskFlow.API.MiddleWare;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<ExceptionHandlingMiddleware> logger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            // Với endpoint streaming (SSE chatbot), response có thể đã gửi byte đầu
            // tiên (event "session") rồi mới lỗi. Lúc này KHÔNG được set StatusCode
            // hay ghi JSON nữa (Kestrel sẽ ném "StatusCode cannot be set because the
            // response has already started" và che mất lỗi gốc). Chỉ log lỗi gốc rồi
            // dừng - phía client sẽ thấy stream đứt (thiếu event "done").
            if (context.Response.HasStarted)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception after the response has already started; cannot change status code."
                );
                return;
            }

            int statusCodes = exception switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                BadRequestException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            // Log lỗi gốc (kể cả 500) để không phải đoán khi debug
            if (statusCodes == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception");
            }

            var message =
                statusCodes == StatusCodes.Status500InternalServerError
                    ? "Something went wrong"
                    : exception.Message;

            context.Response.StatusCode = statusCodes;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    statusCodes,
                    message
                }
            );
        }
    }
}
