using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GymForYou.Api.Tests;

public class PlatformAuthTests
{
    [Fact]
    public async Task Platform_login_with_valid_credentials_returns_token()
    {
        await using var db = BuildDb();
        var service = BuildAuthService(db, "superadmin@gym.local", "SuperAdmin123!");

        var result = await service.PlatformLoginAsync(new PlatformLoginRequest("superadmin@gym.local", "SuperAdmin123!"));

        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.PLATFORM_ADMIN);
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Tenant_login_is_blocked_when_billing_not_paid()
    {
        await using var db = BuildDb();
        var tenant = new Tenant
        {
            Name = "Gym Unpaid",
            Slug = "gym-unpaid",
            BillingStatus = "UNPAID",
            BillingValidUntilUtc = DateTime.UtcNow.AddDays(30)
        };
        var user = new User
        {
            TenantId = tenant.Id,
            Email = "owner@unpaid.local",
            FullName = "Owner",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner123!"),
            Role = UserRole.OWNER
        };
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = BuildAuthService(db, "superadmin@gym.local", "SuperAdmin123!");
        var login = await service.LoginAsync(new LoginRequest(null, tenant.Slug, user.Email, "Owner123!", UserRole.OWNER));

        login.Should().BeNull();
    }

    [Fact]
    public async Task Staff_and_member_login_are_blocked_when_tenant_suspended_and_resume_after_unsuspend()
    {
        await using var db = BuildDb();
        var tenant = new Tenant
        {
            Name = "Gym Susp",
            Slug = "gym-susp-login",
            IsSuspended = true,
            BillingStatus = "PAID",
            BillingValidUntilUtc = DateTime.UtcNow.AddDays(30)
        };
        var owner = new User
        {
            TenantId = tenant.Id,
            Email = "owner@susp.local",
            FullName = "Owner",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner123!"),
            Role = UserRole.OWNER
        };
        var member = new User
        {
            TenantId = tenant.Id,
            Email = "member@susp.local",
            FullName = "Member",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Member123!"),
            Role = UserRole.MEMBER
        };
        db.Tenants.Add(tenant);
        db.Users.AddRange(owner, member);
        await db.SaveChangesAsync();

        var service = BuildAuthService(db, "superadmin@gym.local", "SuperAdmin123!");
        var ownerLoginBlocked = await service.LoginAsync(new LoginRequest(null, tenant.Slug, owner.Email, "Owner123!", UserRole.OWNER));
        var memberLoginBlocked = await service.LoginAsync(new LoginRequest(null, tenant.Slug, member.Email, "Member123!", UserRole.MEMBER));

        ownerLoginBlocked.Should().BeNull();
        memberLoginBlocked.Should().BeNull();

        tenant.IsSuspended = false;
        await db.SaveChangesAsync();

        var ownerLogin = await service.LoginAsync(new LoginRequest(null, tenant.Slug, owner.Email, "Owner123!", UserRole.OWNER));
        var memberLogin = await service.LoginAsync(new LoginRequest(null, tenant.Slug, member.Email, "Member123!", UserRole.MEMBER));

        ownerLogin.Should().NotBeNull();
        memberLogin.Should().NotBeNull();
    }

    private static AppDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, new TenantProvider { TenantId = null });
    }

    private static AuthService BuildAuthService(AppDbContext db, string adminEmail, string adminPassword)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "super-secret-dev-key-change-me-32-bytes-min",
            ["JWT_ISSUER"] = "gymforyou-api",
            ["JWT_AUDIENCE"] = "gymforyou-web",
            ["PLATFORM_ADMIN_EMAIL"] = adminEmail,
            ["PLATFORM_ADMIN_PASSWORD"] = adminPassword
        }).Build();
        var jwt = new JwtTokenService(config);
        return new AuthService(db, jwt, config);
    }
}
