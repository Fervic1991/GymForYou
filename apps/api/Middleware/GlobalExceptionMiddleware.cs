using Microsoft.AspNetCore.Mvc;

namespace GymForYou.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var pd = new ProblemDetails
            {
                Title = "Server error",
                Detail = ex.Message,
                Status = 500,
                Instance = context.Request.Path
            };
            await context.Response.WriteAsJsonAsync(pd);
        }
    }
}
