namespace TaskFlow.Api { }
public class ExceptionHandingMiddleware
{
    private readonly RequestDelegate _next;


    public ExceptionHandingMiddleware(RequestDelegate next)
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
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ArgumentException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            var message = statusCodes == StatusCodes.Status500InternalServerError ? "Some thing was wrong" : exception.Message;

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