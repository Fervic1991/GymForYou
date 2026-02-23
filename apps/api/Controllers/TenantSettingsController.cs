using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("tenant/settings")]
[Authorize]
public class TenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _service;
    private readonly AppDbContext _db;

    public TenantSettingsController(ITenantSettingsService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER,MEMBER")]
    public async Task<IActionResult> Get()
    {
        var settings = await _service.GetOrCreateAsync();
        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        var locale = NormalizeLocale(tenant?.DefaultLocale);
        var primaryColor = string.IsNullOrWhiteSpace(tenant?.PrimaryColor) ? "#22c55e" : tenant!.PrimaryColor;
        var secondaryColor = string.IsNullOrWhiteSpace(tenant?.SecondaryColor) ? "#f97316" : tenant!.SecondaryColor;

        return Ok(new TenantSettingsResponse(
            settings.TenantId,
            tenant?.Name ?? "Palestra",
            settings.CancelCutoffHours,
            settings.MaxNoShows30d,
            settings.WeeklyBookingLimit,
            settings.BookingBlockDays,
            locale,
            primaryColor,
            secondaryColor
        ));
    }

    [HttpPut]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> Update(UpdateTenantSettingsRequest request)
    {
        var settings = await _service.GetOrCreateAsync();
        settings.CancelCutoffHours = request.CancelCutoffHours;
        settings.MaxNoShows30d = request.MaxNoShows30d;
        settings.WeeklyBookingLimit = request.WeeklyBookingLimit;
        settings.BookingBlockDays = request.BookingBlockDays;

        // DbContext tracked entity save through service scope
        await _db.SaveChangesAsync();

        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        var locale = NormalizeLocale(tenant?.DefaultLocale);
        var primaryColor = string.IsNullOrWhiteSpace(tenant?.PrimaryColor) ? "#22c55e" : tenant!.PrimaryColor;
        var secondaryColor = string.IsNullOrWhiteSpace(tenant?.SecondaryColor) ? "#f97316" : tenant!.SecondaryColor;

        return Ok(new TenantSettingsResponse(
            settings.TenantId,
            tenant?.Name ?? "Palestra",
            settings.CancelCutoffHours,
            settings.MaxNoShows30d,
            settings.WeeklyBookingLimit,
            settings.BookingBlockDays,
            locale,
            primaryColor,
            secondaryColor
        ));
    }

    private Guid GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(raw, out var tenantId))
            throw new InvalidOperationException("Tenant claim missing");
        return tenantId;
    }

    private static string NormalizeLocale(string? locale)
        => locale is "it" or "es" ? locale : "it";
}
