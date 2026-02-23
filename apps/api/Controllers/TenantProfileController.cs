using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("tenant")]
[Authorize(Roles = "OWNER,MANAGER")]
public class TenantProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantProfileController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        return Ok(new
        {
            tenantId = tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DefaultLocale,
            tenant.City,
            tenant.Address,
            tenant.Phone,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateTenantProfileRequest request)
    {
        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        tenant.Name = request.Name.Trim();
        tenant.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        tenant.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        tenant.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.PrimaryColor)) tenant.PrimaryColor = request.PrimaryColor;
        if (!string.IsNullOrWhiteSpace(request.SecondaryColor)) tenant.SecondaryColor = request.SecondaryColor;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            tenantId = tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DefaultLocale,
            tenant.City,
            tenant.Address,
            tenant.Phone,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor
        });
    }

    private Guid GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(raw, out var tenantId))
            throw new InvalidOperationException("Tenant claim missing");
        return tenantId;
    }
}
