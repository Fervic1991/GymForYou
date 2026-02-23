using GymForYou.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
public class JoinController : ControllerBase
{
    private readonly AppDbContext _db;

    public JoinController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("join/{code}")]
    [EnableRateLimiting("onboarding")]
    public async Task<IActionResult> ResolveJoin(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest("code required");

        var link = await _db.TenantJoinLinks.IgnoreQueryFilters()
            .Where(x => x.Code == normalized)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (link is null || !link.IsActive)
            return NotFound("Join link not found");

        if (link.ExpiresAtUtc.HasValue && DateTime.UtcNow > link.ExpiresAtUtc.Value)
            return StatusCode(410, new { error = "Join link expired" });

        if (link.MaxUses.HasValue && link.UsesCount >= link.MaxUses.Value)
            return StatusCode(410, new { error = "Join link max uses reached" });

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == link.TenantId);
        if (tenant is null) return NotFound("Tenant not found");
        if (tenant.IsSuspended) return StatusCode(403, new { error = "Tenant suspended" });

        var locale = tenant.DefaultLocale is "it" or "es" ? tenant.DefaultLocale : "it";
        return Ok(new { tenantName = tenant.Name, defaultLocale = locale, status = "ACTIVE" });
    }

    [HttpGet("tenants/resolve")]
    [EnableRateLimiting("onboarding")]
    public async Task<IActionResult> ResolveTenantBySlug([FromQuery] string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest("slug required");

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Slug == normalized);

        if (tenant is null) return NotFound("Tenant not found");
        if (tenant.IsSuspended) return StatusCode(403, new { error = "Tenant suspended" });

        var locale = tenant.DefaultLocale is "it" or "es" ? tenant.DefaultLocale : "it";
        return Ok(new { tenantId = tenant.Id, tenantName = tenant.Name, defaultLocale = locale, status = "ACTIVE" });
    }
}
