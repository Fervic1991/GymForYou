using GymForYou.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("tenant/join-link")]
[Authorize(Roles = "OWNER,MANAGER")]
public class TenantJoinLinkController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public TenantJoinLinkController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var tenantId = GetTenantId();
        var active = await _db.TenantJoinLinks
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (active is null)
        {
            active = new Models.TenantJoinLink
            {
                TenantId = tenantId,
                Code = await GenerateUniqueCodeAsync(),
                IsActive = true
            };
            _db.TenantJoinLinks.Add(active);
            await _db.SaveChangesAsync();
        }

        var webBase = (_config["WEB_BASE_URL"] ?? "http://localhost:13000").TrimEnd('/');
        return Ok(new
        {
            active.Code,
            Url = $"{webBase}/join/{active.Code}",
            active.ExpiresAtUtc,
            active.MaxUses,
            active.UsesCount,
            active.IsActive
        });
    }

    [HttpPost("rotate")]
    public async Task<IActionResult> Rotate()
    {
        var tenantId = GetTenantId();
        var current = await _db.TenantJoinLinks
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
        foreach (var c in current) c.IsActive = false;

        var created = new Models.TenantJoinLink
        {
            TenantId = tenantId,
            Code = await GenerateUniqueCodeAsync(),
            IsActive = true
        };
        _db.TenantJoinLinks.Add(created);
        await _db.SaveChangesAsync();

        var webBase = (_config["WEB_BASE_URL"] ?? "http://localhost:13000").TrimEnd('/');
        return Ok(new { created.Code, Url = $"{webBase}/join/{created.Code}" });
    }

    private Guid GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(raw, out var tenantId))
            throw new InvalidOperationException("Tenant claim missing");
        return tenantId;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        while (true)
        {
            var code = Convert.ToHexString(Guid.NewGuid().ToByteArray()).Substring(0, 8).ToUpperInvariant();
            var exists = await _db.TenantJoinLinks.IgnoreQueryFilters().AnyAsync(x => x.Code == code);
            if (!exists) return code;
        }
    }
}
