using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("platform/tenants")]
public class PlatformAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public PlatformAdminController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private bool IsPlatformAdmin(HttpRequest req)
    {
        var byJwt = User?.Identity?.IsAuthenticated == true &&
                    User.IsInRole(UserRole.PLATFORM_ADMIN.ToString());
        if (byJwt) return true;

        var expected = _config["PLATFORM_ADMIN_KEY"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return req.Headers["X-Platform-Admin-Key"] == expected;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListTenants()
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();

        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.DefaultLocale,
                x.LogoUrl,
                x.City,
                x.BillingStatus,
                x.BillingValidUntilUtc,
                Status = x.IsSuspended ? "SUSPENDED" : "ACTIVE"
            })
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("{tenantId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTenant(Guid tenantId)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();
        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DefaultLocale,
            tenant.JoinCode,
            tenant.City,
            tenant.Address,
            tenant.Phone,
            tenant.BillingStatus,
            tenant.BillingValidUntilUtc,
            Status = tenant.IsSuspended ? "SUSPENDED" : "ACTIVE",
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == request.Slug))
            return Conflict("Tenant slug already used");

        var tenant = new Tenant
        {
            Name = request.Name,
            Slug = request.Slug,
            LogoUrl = request.LogoUrl,
            PrimaryColor = request.PrimaryColor ?? "#0ea5e9",
            SecondaryColor = request.SecondaryColor ?? "#111827",
            DefaultLocale = "it",
            City = request.City,
            Address = request.Address,
            Phone = request.Phone,
            IsSuspended = false,
            BillingStatus = "PAID",
            BillingValidUntilUtc = DateTime.UtcNow.AddMonths(1),
            BillingLastUpdatedAtUtc = DateTime.UtcNow
        };

        tenant.JoinCode = await GenerateUniqueJoinCodeAsync();

        var owner = new User
        {
            TenantId = tenant.Id,
            FullName = request.OwnerName,
            Email = request.OwnerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.OwnerPassword),
            Role = UserRole.OWNER
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();
        return Ok(new { TenantId = tenant.Id, OwnerUserId = owner.Id, DefaultLocale = tenant.DefaultLocale, tenant.Slug, tenant.JoinCode });
    }

    [HttpPatch("{tenantId:guid}/locale")]
    [Authorize]
    public async Task<IActionResult> UpdateTenantLocale(Guid tenantId, UpdateTenantLocaleRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        tenant.DefaultLocale = request.DefaultLocale;
        await _db.SaveChangesAsync();

        return Ok(new { TenantId = tenant.Id, DefaultLocale = tenant.DefaultLocale });
    }

    [HttpPatch("{tenantId:guid}/billing")]
    [Authorize]
    public async Task<IActionResult> UpdateTenantBilling(Guid tenantId, UpdateTenantBillingRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        tenant.BillingStatus = request.BillingStatus;
        tenant.BillingValidUntilUtc = request.BillingValidUntilUtc.HasValue
            ? DateTime.SpecifyKind(request.BillingValidUntilUtc.Value, DateTimeKind.Utc)
            : null;
        tenant.BillingLastUpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new
        {
            tenantId = tenant.Id,
            tenant.BillingStatus,
            tenant.BillingValidUntilUtc,
            tenant.BillingLastUpdatedAtUtc
        });
    }

    [HttpPatch("{tenantId:guid}/suspension")]
    [Authorize]
    public async Task<IActionResult> UpdateTenantSuspension(Guid tenantId, UpdateTenantSuspensionRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        tenant.IsSuspended = request.IsSuspended;
        await _db.SaveChangesAsync();
        return Ok(new { tenantId = tenant.Id, tenant.IsSuspended });
    }

    [HttpGet("overview")]
    [Authorize]
    public async Task<IActionResult> Overview()
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.City,
                x.IsSuspended,
                x.BillingStatus,
                x.BillingValidUntilUtc
            })
            .ToListAsync();

        var tenantIds = tenants.Select(x => x.Id).ToList();

        var memberCounts = await _db.Users.IgnoreQueryFilters()
            .Where(x => x.Role == UserRole.MEMBER && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var classCounts = await _db.GymClasses.IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var revenueByTenant = await _db.Payments.IgnoreQueryFilters()
            .Where(x => x.Status == "paid" && x.CreatedAtUtc >= monthStart && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.TenantId, x => x.Amount);

        var tenantStats = tenants.Select(t =>
        {
            var billingActive = string.Equals(t.BillingStatus, "PAID", StringComparison.OrdinalIgnoreCase)
                                && (!t.BillingValidUntilUtc.HasValue || t.BillingValidUntilUtc.Value >= now);
            var status = t.IsSuspended ? "SUSPENDED" : billingActive ? "ACTIVE" : "EXPIRED";
            return new
            {
                tenantId = t.Id,
                t.Name,
                t.Slug,
                t.City,
                status,
                t.BillingStatus,
                t.BillingValidUntilUtc,
                members = memberCounts.GetValueOrDefault(t.Id, 0),
                classes = classCounts.GetValueOrDefault(t.Id, 0),
                revenueMonth = revenueByTenant.GetValueOrDefault(t.Id, 0m)
            };
        }).ToList();

        return Ok(new
        {
            totals = new
            {
                tenants = tenantStats.Count,
                activeTenants = tenantStats.Count(x => x.status == "ACTIVE"),
                expiredTenants = tenantStats.Count(x => x.status == "EXPIRED"),
                suspendedTenants = tenantStats.Count(x => x.status == "SUSPENDED"),
                members = tenantStats.Sum(x => x.members),
                revenueMonth = tenantStats.Sum(x => x.revenueMonth)
            },
            tenants = tenantStats
        });
    }

    [HttpGet("{tenantId:guid}/members")]
    [Authorize]
    public async Task<IActionResult> TenantMembers(Guid tenantId)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var members = await _db.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == UserRole.MEMBER)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName, x.Email, x.Phone, x.IsActive, x.CreatedAtUtc })
            .ToListAsync();
        return Ok(members);
    }

    [HttpGet("{tenantId:guid}/classes")]
    [Authorize]
    public async Task<IActionResult> TenantClasses(Guid tenantId)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var classes = await _db.GymClasses.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Title)
            .Select(x => new { x.Id, x.Title, x.Description, x.Capacity, x.RecurrenceRule, x.TrainerUserId })
            .ToListAsync();
        return Ok(classes);
    }

    [HttpGet("{tenantId:guid}/staff")]
    [Authorize]
    public async Task<IActionResult> TenantStaff(Guid tenantId)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var rows = await _db.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role != UserRole.MEMBER)
            .OrderByDescending(x => x.Role == UserRole.OWNER)
            .ThenBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Email,
                x.Role,
                x.IsActive
            })
            .ToListAsync();
        return Ok(rows);
    }

    [HttpPatch("{tenantId:guid}/staff/{userId:guid}/role")]
    [Authorize]
    public async Task<IActionResult> UpdateStaffRole(Guid tenantId, Guid userId, UpdatePlatformStaffRoleRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        if (request.Role == UserRole.MEMBER || request.Role == UserRole.PLATFORM_ADMIN)
            return BadRequest("Role not allowed");

        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);
        if (user is null) return NotFound();
        if (user.Role == UserRole.MEMBER) return BadRequest("Cannot manage member role here");

        if (request.Role == UserRole.OWNER)
        {
            var activeOwners = await _db.Users.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Role == UserRole.OWNER && x.IsActive && x.Id != userId)
                .CountAsync();
            if (activeOwners > 0)
                return Conflict("Only one active OWNER is allowed");
        }

        user.Role = request.Role;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.TenantId, user.Role, user.IsActive });
    }

    [HttpPatch("{tenantId:guid}/staff/{userId:guid}/disable")]
    [Authorize]
    public async Task<IActionResult> DisableStaff(Guid tenantId, Guid userId, UpdatePlatformStaffDisabledRequest request)
    {
        if (!IsPlatformAdmin(Request)) return Unauthorized();
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);
        if (user is null) return NotFound();
        if (user.Role == UserRole.MEMBER) return BadRequest("Cannot manage member status here");

        if (!request.IsActive && user.Role == UserRole.OWNER)
        {
            var otherActiveOwners = await _db.Users.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Role == UserRole.OWNER && x.IsActive && x.Id != user.Id)
                .CountAsync();
            if (otherActiveOwners == 0)
                return Conflict("Cannot disable the only active OWNER");
        }

        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.TenantId, user.Role, user.IsActive });
    }

    private async Task<string> GenerateUniqueJoinCodeAsync()
    {
        while (true)
        {
            var code = Convert.ToHexString(Guid.NewGuid().ToByteArray()).Substring(0, 8).ToUpperInvariant();
            var exists = await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.JoinCode == code);
            if (!exists) return code;
        }
    }
}
