using System.Security.Claims;
using GymForYou.Api.Infrastructure;

namespace GymForYou.Api.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITenantProvider tenantProvider)
    {
        string? tenantRaw = context.User.FindFirstValue("tenant_id");

        if (string.IsNullOrWhiteSpace(tenantRaw) && context.Request.Headers.TryGetValue("X-Tenant-Id", out var header))
            tenantRaw = header.ToString();

        if (Guid.TryParse(tenantRaw, out var tenantId))
            tenantProvider.TenantId = tenantId;

        await _next(context);
    }
}
