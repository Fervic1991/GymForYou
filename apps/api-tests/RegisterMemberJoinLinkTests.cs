using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GymForYou.Api.Tests;

public class RegisterMemberJoinLinkTests
{
    [Fact]
    public async Task Register_with_joinCode_creates_member_in_correct_tenant()
    {
        var db = await CreateDbAsync();
        var tenant = await AddTenantAsync(db, "gym-a");
        await AddJoinLinkAsync(db, tenant.Id, "CODEA12");
        var controller = BuildAuthController(db);

        var result = await controller.RegisterMember(new RegisterMemberRequest(
            JoinCode: "CODEA12",
            FullName: "Mario Rossi",
            Email: "mario@gym.com",
            Phone: "123",
            Password: "Member123!"
        ));

        result.Result.Should().BeOfType<OkObjectResult>();
        (await db.Users.IgnoreQueryFilters().Where(x => x.Email == "mario@gym.com").Select(x => x.TenantId).SingleAsync()).Should().Be(tenant.Id);
    }

    [Fact]
    public async Task Rotate_invalidates_old_code()
    {
        var tenantId = Guid.NewGuid();
        var db = await CreateDbAsync(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Gym", Slug = "gym-rotate", DefaultLocale = "it" });
        db.TenantJoinLinks.Add(new TenantJoinLink { TenantId = tenantId, Code = "OLD12345", IsActive = true });
        await db.SaveChangesAsync();

        var controller = BuildJoinLinkController(db, tenantId, "OWNER");
        var rotateResult = await controller.Rotate();
        rotateResult.Should().BeOfType<OkObjectResult>();

        var old = await db.TenantJoinLinks.IgnoreQueryFilters().FirstAsync(x => x.Code == "OLD12345");
        old.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Register_should_fail_when_maxUses_reached()
    {
        var db = await CreateDbAsync();
        var tenant = await AddTenantAsync(db, "gym-max");
        await AddJoinLinkAsync(db, tenant.Id, "MAX11111", maxUses: 1, usesCount: 1);
        var controller = BuildAuthController(db);

        var result = await controller.RegisterMember(new RegisterMemberRequest("MAX11111", "Mario A", "a@gym.com", null, "Member123!"));
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Register_should_fail_when_link_expired()
    {
        var db = await CreateDbAsync();
        var tenant = await AddTenantAsync(db, "gym-exp");
        await AddJoinLinkAsync(db, tenant.Id, "EXP11111", expiresAt: DateTime.UtcNow.AddMinutes(-1));
        var controller = BuildAuthController(db);

        var result = await controller.RegisterMember(new RegisterMemberRequest("EXP11111", "Mario A", "a@gym.com", null, "Member123!"));
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Register_should_fail_when_tenant_suspended()
    {
        var db = await CreateDbAsync();
        var tenant = await AddTenantAsync(db, "gym-susp", isSuspended: true);
        await AddJoinLinkAsync(db, tenant.Id, "SUSP1111");
        var controller = BuildAuthController(db);

        var result = await controller.RegisterMember(new RegisterMemberRequest("SUSP1111", "Mario A", "a@gym.com", null, "Member123!"));
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    private static AuthController BuildAuthController(AppDbContext db)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = "this_is_a_test_secret_with_minimum_length_123456",
            ["JWT_ISSUER"] = "gym.test",
            ["JWT_AUDIENCE"] = "gym.test.clients"
        }).Build();

        var jwt = new JwtTokenService(config);
        var service = new AuthService(db, jwt, config);
        return new AuthController(service);
    }

    private static TenantJoinLinkController BuildJoinLinkController(AppDbContext db, Guid tenantId, string role)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WEB_BASE_URL"] = "http://localhost:13000"
        }).Build();
        var controller = new TenantJoinLinkController(db, cfg)
        {
            ControllerContext = BuildContext(tenantId, Guid.NewGuid(), role)
        };
        return controller;
    }

    private static ControllerContext BuildContext(Guid tenantId, Guid userId, string role)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
            new(System.Security.Claims.ClaimTypes.Role, role)
        };
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "test"));
        return new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal } };
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenantContext });
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Tenant> AddTenantAsync(AppDbContext db, string slug, bool isSuspended = false)
    {
        var tenant = new Tenant { Name = slug, Slug = slug, DefaultLocale = "it", IsSuspended = isSuspended };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static async Task AddJoinLinkAsync(AppDbContext db, Guid tenantId, string code, bool isActive = true, DateTime? expiresAt = null, int? maxUses = null, int usesCount = 0)
    {
        db.TenantJoinLinks.Add(new TenantJoinLink
        {
            TenantId = tenantId,
            Code = code,
            IsActive = isActive,
            ExpiresAtUtc = expiresAt,
            MaxUses = maxUses,
            UsesCount = usesCount
        });
        await db.SaveChangesAsync();
    }
}
