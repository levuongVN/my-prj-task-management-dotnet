using TaskFlow.Application.Common;

namespace TaskFlow.API.MiddleWare;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
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
