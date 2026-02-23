using GymForYou.Api.Data;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Middleware;

public class TenantSuspensionMiddleware
{
    private static readonly HashSet<PathString> PublicAllowedPrefixes = new()
    {
        new PathString("/platform"),
        new PathString("/stripe")
    };

    private readonly RequestDelegate _next;

    public TenantSuspensionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, AppDbContext db)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tenantRaw = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantRaw, out var tenantId))
        {
            await _next(context);
            return;
        }

        var isSuspended = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.IsSuspended)
            .FirstOrDefaultAsync();

        if (isSuspended)
        {
            await WriteSuspendedProblem(context);
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(PathString path)
    {
        foreach (var prefix in PublicAllowedPrefixes)
        {
            if (path.StartsWithSegments(prefix)) return true;
        }
        return false;
    }

    private static async Task WriteSuspendedProblem(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        var pd = new ProblemDetails
        {
            Title = "Tenant suspended",
            Detail = "Tenant suspended",
            Status = StatusCodes.Status403Forbidden,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsJsonAsync(pd);
    }
}

