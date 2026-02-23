using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace GymForYou.Api.Tests;

public class RenewalReminderServiceTests
{
    [Fact]
    public async Task Renewal_service_should_not_send_duplicates_in_same_day()
    {
        var tenantId = Guid.NewGuid();
        var db = await CreateDbAsync(tenantId);
        var member = await SeedExpiringMemberAsync(db, tenantId, "member@renew.local");
        var service = BuildService(db);

        var first = await service.SendForTenantAsync(tenantId);
        var second = await service.SendForTenantAsync(tenantId);

        first.Should().Be(1);
        second.Should().Be(0);
        (await db.NotificationLogs.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && x.Type == "renewal_reminder" && x.ToEmail == member.Email))
            .Should().Be(1);
    }

    [Fact]
    public async Task Renewal_service_should_skip_suspended_tenants_in_background_mode()
    {
        var tenantActive = Guid.NewGuid();
        var tenantSusp = Guid.NewGuid();
        var db = await CreateDbAsync(null);
        db.Tenants.AddRange(
            new Tenant { Id = tenantActive, Name = "Gym A", Slug = "gym-a", DefaultLocale = "it", IsSuspended = false },
            new Tenant { Id = tenantSusp, Name = "Gym S", Slug = "gym-s", DefaultLocale = "it", IsSuspended = true });
        await db.SaveChangesAsync();

        await SeedExpiringMemberAsync(db, tenantActive, "active@renew.local");
        await SeedExpiringMemberAsync(db, tenantSusp, "susp@renew.local");

        var service = BuildService(db);
        var sent = await service.SendForAllTenantsAsync();

        sent.Should().Be(1);
        (await db.NotificationLogs.IgnoreQueryFilters().CountAsync(x => x.ToEmail == "active@renew.local")).Should().Be(1);
        (await db.NotificationLogs.IgnoreQueryFilters().CountAsync(x => x.ToEmail == "susp@renew.local")).Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_kpis_should_include_expiring_members_count()
    {
        var tenantId = Guid.NewGuid();
        var db = await CreateDbAsync(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Gym KPI", Slug = "gym-kpi", DefaultLocale = "it", BillingStatus = "PAID", BillingValidUntilUtc = DateTime.UtcNow.AddDays(30) });
        await db.SaveChangesAsync();

        await SeedExpiringMemberAsync(db, tenantId, "m1@kpi.local");
        await SeedExpiringMemberAsync(db, tenantId, "m2@kpi.local");

        var controller = new DashboardController(db)
        {
            ControllerContext = BuildContext(tenantId, Guid.NewGuid(), "OWNER")
        };

        var result = await controller.Kpis();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"expiringMembers\":2");
    }

    private static RenewalReminderService BuildService(AppDbContext db)
    {
        var notification = new NotificationService(NullLogger<NotificationService>.Instance, db);
        return new RenewalReminderService(db, notification, NullLogger<RenewalReminderService>.Instance);
    }

    private static async Task<User> SeedExpiringMemberAsync(AppDbContext db, Guid tenantId, string email)
    {
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Gym", Slug = $"gym-{Guid.NewGuid():N}".Substring(0, 8), DefaultLocale = "it", BillingStatus = "PAID", BillingValidUntilUtc = DateTime.UtcNow.AddDays(30) });
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            FullName = email,
            PasswordHash = "x",
            Role = UserRole.MEMBER,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var plan = new MembershipPlan
        {
            TenantId = tenantId,
            Name = "Mensile",
            Description = "",
            Price = 49m,
            Interval = "monthly",
            IsActive = true
        };
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        db.MemberSubscriptions.Add(new MemberSubscription
        {
            TenantId = tenantId,
            MemberUserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.ACTIVE,
            StartedAtUtc = DateTime.UtcNow.AddDays(-23),
            EndsAtUtc = DateTime.UtcNow.AddDays(3)
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenantId });
        await db.Database.EnsureCreatedAsync();
        return db;
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
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
