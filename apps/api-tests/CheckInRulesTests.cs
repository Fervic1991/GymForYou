using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class CheckInRulesTests
{
    [Fact]
    public async Task Should_reject_checkin_when_subscription_not_active()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        var user = new User { TenantId = tenant, Email = "m@g.com", FullName = "M", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.Add(user);
        db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = user.Id, Status = MemberStatus.ACTIVE, CheckInCode = "ABC" });
        await db.SaveChangesAsync();

        var sut = new CheckInService(db);

        await FluentActions.Invoking(() => sut.CheckInByCodeAsync("ABC", "qr"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_reject_checkin_when_member_suspended()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        var user = new User { TenantId = tenant, Email = "m@g.com", FullName = "M", PasswordHash = "x", Role = UserRole.MEMBER };
        var plan = new MembershipPlan { TenantId = tenant, Name = "P", Description = "", Price = 1, Interval = "monthly" };
        db.Users.Add(user);
        db.MembershipPlans.Add(plan);
        db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = user.Id, Status = MemberStatus.SUSPENDED, CheckInCode = "ABC" });
        db.MemberSubscriptions.Add(new MemberSubscription { TenantId = tenant, MemberUserId = user.Id, PlanId = plan.Id, Status = SubscriptionStatus.ACTIVE, EndsAtUtc = DateTime.UtcNow.AddDays(5) });
        await db.SaveChangesAsync();

        var sut = new CheckInService(db);

        await FluentActions.Invoking(() => sut.CheckInByCodeAsync("ABC", "qr"))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
