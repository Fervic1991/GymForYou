using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Tests;

public class TenantLocaleDefaultsTests
{
    [Fact]
    public async Task Tenant_settings_get_returns_it_when_tenant_locale_missing()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var db = new AppDbContext(options, new TenantProvider { TenantId = tenantId });
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Gym",
            Slug = "gym-default-it",
            DefaultLocale = ""
        });
        db.TenantSettings.Add(new TenantSettings
        {
            TenantId = tenantId,
            CancelCutoffHours = 6,
            MaxNoShows30d = 3,
            WeeklyBookingLimit = 8,
            BookingBlockDays = 7
        });
        await db.SaveChangesAsync();

        var service = new TenantSettingsService(db);
        var controller = new TenantSettingsController(service, db)
        {
            ControllerContext = BuildContext(tenantId, Guid.NewGuid(), "OWNER")
        };

        var result = await controller.Get();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<TenantSettingsResponse>().Subject;
        response.DefaultLocale.Should().Be("it");
    }

    private static ControllerContext BuildContext(Guid tenantId, Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
