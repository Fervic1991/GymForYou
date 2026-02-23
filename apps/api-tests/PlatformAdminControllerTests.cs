using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GymForYou.Api.Tests;

public class PlatformAdminControllerTests
{
    [Fact]
    public async Task CreateTenant_without_admin_key_returns_unauthorized()
    {
        var db = CreateDb();
        var controller = BuildController(db, "secret-key", headerValue: null);

        var result = await controller.CreateTenant(BuildRequest("gym-no-key"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateTenant_with_wrong_admin_key_returns_unauthorized()
    {
        var db = CreateDb();
        var controller = BuildController(db, "secret-key", headerValue: "wrong-key");

        var result = await controller.CreateTenant(BuildRequest("gym-wrong-key"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateTenant_with_valid_admin_key_creates_tenant_and_owner()
    {
        var db = CreateDb();
        var controller = BuildController(db, "secret-key", headerValue: "secret-key");
        var req = BuildRequest("gym-valid-key");

        var result = await controller.CreateTenant(req);

        result.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.IgnoreQueryFilters().CountAsync(x => x.Slug == req.Slug)).Should().Be(1);
        (await db.Users.IgnoreQueryFilters().CountAsync(x => x.Email == req.OwnerEmail)).Should().Be(1);
        (await db.Tenants.IgnoreQueryFilters().Where(x => x.Slug == req.Slug).Select(x => x.DefaultLocale).SingleAsync()).Should().Be("it");
    }

    [Fact]
    public async Task Platform_admin_can_change_tenant_locale()
    {
        var db = CreateDb();
        var tenant = new GymForYou.Api.Models.Tenant { Name = "Gym", Slug = "gym-locale", DefaultLocale = "it" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: "secret-key");
        var result = await controller.UpdateTenantLocale(tenant.Id, new UpdateTenantLocaleRequest("es"));

        result.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenant.Id).Select(x => x.DefaultLocale).SingleAsync()).Should().Be("es");
    }

    [Fact]
    public async Task Staff_non_platform_cannot_change_tenant_locale()
    {
        var db = CreateDb();
        var tenant = new GymForYou.Api.Models.Tenant { Name = "Gym", Slug = "gym-locale-blocked", DefaultLocale = "it" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: null);
        var result = await controller.UpdateTenantLocale(tenant.Id, new UpdateTenantLocaleRequest("es"));

        result.Should().BeOfType<UnauthorizedResult>();
        (await db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenant.Id).Select(x => x.DefaultLocale).SingleAsync()).Should().Be("it");
    }

    [Fact]
    public async Task Platform_admin_can_suspend_and_unsuspend_tenant()
    {
        var db = CreateDb();
        var tenant = new GymForYou.Api.Models.Tenant { Name = "Gym Suspend", Slug = "gym-suspend", DefaultLocale = "it", IsSuspended = false };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: "secret-key");
        var suspend = await controller.UpdateTenantSuspension(tenant.Id, new UpdateTenantSuspensionRequest(true));
        suspend.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenant.Id).Select(x => x.IsSuspended).SingleAsync()).Should().BeTrue();

        var unsuspend = await controller.UpdateTenantSuspension(tenant.Id, new UpdateTenantSuspensionRequest(false));
        unsuspend.Should().BeOfType<OkObjectResult>();
        (await db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenant.Id).Select(x => x.IsSuspended).SingleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Platform_admin_can_list_and_update_staff_cross_tenant()
    {
        var db = CreateDb();
        var tenant = new GymForYou.Api.Models.Tenant { Name = "Gym Staff", Slug = "gym-staff", DefaultLocale = "it" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var owner = new GymForYou.Api.Models.User { TenantId = tenant.Id, FullName = "Owner", Email = "owner@gym.staff", PasswordHash = "x", Role = GymForYou.Api.Models.UserRole.OWNER, IsActive = true };
        var trainer = new GymForYou.Api.Models.User { TenantId = tenant.Id, FullName = "Trainer", Email = "trainer@gym.staff", PasswordHash = "x", Role = GymForYou.Api.Models.UserRole.TRAINER, IsActive = true };
        db.Users.AddRange(owner, trainer);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: "secret-key");
        var list = await controller.TenantStaff(tenant.Id);
        list.Should().BeOfType<OkObjectResult>();

        var roleResult = await controller.UpdateStaffRole(tenant.Id, trainer.Id, new UpdatePlatformStaffRoleRequest(GymForYou.Api.Models.UserRole.MANAGER));
        roleResult.Should().BeOfType<OkObjectResult>();
        (await db.Users.IgnoreQueryFilters().Where(x => x.Id == trainer.Id).Select(x => x.Role).SingleAsync())
            .Should().Be(GymForYou.Api.Models.UserRole.MANAGER);

        var disableResult = await controller.DisableStaff(tenant.Id, trainer.Id, new UpdatePlatformStaffDisabledRequest(false));
        disableResult.Should().BeOfType<OkObjectResult>();
        (await db.Users.IgnoreQueryFilters().Where(x => x.Id == trainer.Id).Select(x => x.IsActive).SingleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Non_platform_cannot_manage_cross_tenant_staff()
    {
        var db = CreateDb();
        var tenant = new GymForYou.Api.Models.Tenant { Name = "Gym Block", Slug = "gym-block", DefaultLocale = "it" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var staff = new GymForYou.Api.Models.User { TenantId = tenant.Id, FullName = "Staff", Email = "staff@gym.block", PasswordHash = "x", Role = GymForYou.Api.Models.UserRole.MANAGER };
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: null);
        (await controller.TenantStaff(tenant.Id)).Should().BeOfType<UnauthorizedResult>();
        (await controller.UpdateStaffRole(tenant.Id, staff.Id, new UpdatePlatformStaffRoleRequest(GymForYou.Api.Models.UserRole.TRAINER))).Should().BeOfType<UnauthorizedResult>();
        (await controller.DisableStaff(tenant.Id, staff.Id, new UpdatePlatformStaffDisabledRequest(false))).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Cross_tenant_staff_update_returns_not_found()
    {
        var db = CreateDb();
        var tenantA = new GymForYou.Api.Models.Tenant { Name = "Gym A", Slug = "gym-a-x", DefaultLocale = "it" };
        var tenantB = new GymForYou.Api.Models.Tenant { Name = "Gym B", Slug = "gym-b-x", DefaultLocale = "it" };
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();

        var staffA = new GymForYou.Api.Models.User
        {
            TenantId = tenantA.Id,
            FullName = "Staff A",
            Email = "staffa@gym.local",
            PasswordHash = "x",
            Role = GymForYou.Api.Models.UserRole.TRAINER,
            IsActive = true
        };
        db.Users.Add(staffA);
        await db.SaveChangesAsync();

        var controller = BuildController(db, "secret-key", headerValue: "secret-key");
        var result = await controller.UpdateStaffRole(tenantB.Id, staffA.Id, new UpdatePlatformStaffRoleRequest(GymForYou.Api.Models.UserRole.MANAGER));
        result.Should().BeOfType<NotFoundResult>();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options, new TenantProvider { TenantId = null });
    }

    private static PlatformAdminController BuildController(AppDbContext db, string configuredKey, string? headerValue)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PLATFORM_ADMIN_KEY"] = configuredKey
        }).Build();

        var controller = new PlatformAdminController(db, cfg)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        if (!string.IsNullOrWhiteSpace(headerValue))
            controller.Request.Headers["X-Platform-Admin-Key"] = headerValue;

        return controller;
    }

    private static CreateTenantRequest BuildRequest(string slug) => new(
        Name: $"Gym {slug}",
        Slug: slug,
        LogoUrl: null,
        PrimaryColor: "#0ea5e9",
        SecondaryColor: "#111827",
        OwnerName: "Owner",
        OwnerEmail: $"{slug}@owner.local",
        OwnerPassword: "Owner123!");
}
