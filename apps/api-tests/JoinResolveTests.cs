using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class JoinResolveTests
{
    [Fact]
    public async Task Join_with_valid_code_returns_tenant_info()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options, new TenantProvider { TenantId = null });
        var tenant = new Tenant { Name = "Gym A", Slug = "gym-a", DefaultLocale = "it" };
        db.Tenants.Add(tenant);
        db.TenantJoinLinks.Add(new TenantJoinLink { TenantId = tenant.Id, Code = "JOINA123", IsActive = true });
        await db.SaveChangesAsync();

        var controller = new JoinController(db);
        var result = await controller.ResolveJoin("JOINA123");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Join_with_suspended_tenant_returns_forbidden()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options, new TenantProvider { TenantId = null });
        var tenant = new Tenant { Name = "Gym S", Slug = "gym-s", DefaultLocale = "it", IsSuspended = true };
        db.Tenants.Add(tenant);
        db.TenantJoinLinks.Add(new TenantJoinLink { TenantId = tenant.Id, Code = "JOINS123", IsActive = true });
        await db.SaveChangesAsync();

        var controller = new JoinController(db);
        var result = await controller.ResolveJoin("JOINS123");

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }
}
