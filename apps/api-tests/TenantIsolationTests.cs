using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Should_throw_when_entity_has_different_tenant()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantProvider = new TenantProvider { TenantId = Guid.NewGuid() };
        var db = new AppDbContext(options, tenantProvider);

        db.Users.Add(new User
        {
            TenantId = Guid.NewGuid(),
            Email = "x@g.com",
            FullName = "X",
            PasswordHash = "x",
            Role = UserRole.MEMBER
        });

        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
}
