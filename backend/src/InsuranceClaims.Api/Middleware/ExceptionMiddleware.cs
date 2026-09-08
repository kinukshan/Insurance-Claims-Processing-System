namespace InsuranceClaims.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // TODO: Implement proper exception handling and logging
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "An unexpected error occurred.",
                message = ex.Message
            });
        }
    }
}
