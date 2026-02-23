using System.Security.Claims;
using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class MemberBookingCancelTests
{
    [Fact]
    public async Task Member_can_cancel_own_booking()
    {
        var tenant = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (db, bookingId) = await BuildScenarioAsync(tenant, memberId, memberId);

        var controller = BuildController(db, tenant, memberId);
        var result = await controller.Cancel(bookingId);

        result.Should().BeOfType<OkObjectResult>();
        var booking = await db.Bookings.FirstAsync(x => x.Id == bookingId);
        (booking.Status == BookingStatus.CANCELED || booking.Status == BookingStatus.LATE_CANCEL).Should().BeTrue();
    }

    [Fact]
    public async Task Member_cannot_cancel_other_user_booking_same_tenant()
    {
        var tenant = Guid.NewGuid();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var (db, bookingId) = await BuildScenarioAsync(tenant, me, other);

        var controller = BuildController(db, tenant, me);
        var result = await controller.Cancel(bookingId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Member_cannot_cancel_booking_from_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var me = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dbTenantB = new AppDbContext(options, new TenantProvider { TenantId = tenantB });

        dbTenantB.TenantSettings.Add(new TenantSettings { TenantId = tenantB, CancelCutoffHours = 6, WeeklyBookingLimit = 8, MaxNoShows30d = 3, BookingBlockDays = 7 });
        var memberOtherTenant = new User { TenantId = tenantB, Id = Guid.NewGuid(), Email = "b@g.com", FullName = "B", PasswordHash = "x", Role = UserRole.MEMBER };
        var trainerOtherTenant = new User { TenantId = tenantB, Id = Guid.NewGuid(), Email = "tb@g.com", FullName = "TB", PasswordHash = "x", Role = UserRole.TRAINER };
        var gymClass = new GymClass { TenantId = tenantB, Title = "C", Description = "", TrainerUserId = trainerOtherTenant.Id, Capacity = 10, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenantB, GymClassId = gymClass.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var booking = new Booking { TenantId = tenantB, SessionId = session.Id, MemberUserId = memberOtherTenant.Id, Status = BookingStatus.BOOKED };
        dbTenantB.Users.AddRange(memberOtherTenant, trainerOtherTenant);
        dbTenantB.GymClasses.Add(gymClass);
        dbTenantB.ClassSessions.Add(session);
        dbTenantB.Bookings.Add(booking);
        await dbTenantB.SaveChangesAsync();

        var dbTenantA = new AppDbContext(options, new TenantProvider { TenantId = tenantA });
        var bookingService = new BookingService(dbTenantA, new TenantSettingsService(dbTenantA));
        var controller = new BookingsController(dbTenantA, bookingService, new TestNotificationService(), new TenantSettingsService(dbTenantA));
        controller.ControllerContext = BuildContext(tenantA, me);

        var result = await controller.Cancel(booking.Id);
        result.Should().BeOfType<NotFoundResult>();
    }

    private static async Task<(AppDbContext db, Guid bookingId)> BuildScenarioAsync(Guid tenant, Guid requesterMemberId, Guid bookingOwnerMemberId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, CancelCutoffHours = 6, WeeklyBookingLimit = 8, MaxNoShows30d = 3, BookingBlockDays = 7 });

        var requester = new User { TenantId = tenant, Id = requesterMemberId, Email = "me@g.com", FullName = "Me", PasswordHash = "x", Role = UserRole.MEMBER };
        var owner = bookingOwnerMemberId == requesterMemberId
            ? requester
            : new User { TenantId = tenant, Id = bookingOwnerMemberId, Email = "other@g.com", FullName = "Other", PasswordHash = "x", Role = UserRole.MEMBER };

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };

        db.Users.Add(requester);
        if (owner != requester) db.Users.Add(owner);
        db.Users.Add(trainer);

        db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = requester.Id, CheckInCode = "MECODE" });
        if (owner != requester) db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = owner.Id, CheckInCode = "OTHCODE" });

        var gymClass = new GymClass { TenantId = tenant, Title = "Yoga", Description = "", TrainerUserId = trainer.Id, Capacity = 10, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = gymClass.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var booking = new Booking { TenantId = tenant, SessionId = session.Id, MemberUserId = owner.Id, Status = BookingStatus.BOOKED };

        db.GymClasses.Add(gymClass);
        db.ClassSessions.Add(session);
        db.Bookings.Add(booking);

        await db.SaveChangesAsync();
        return (db, booking.Id);
    }

    private static BookingsController BuildController(AppDbContext db, Guid tenantId, Guid memberUserId)
    {
        var bookingService = new BookingService(db, new TenantSettingsService(db));
        var controller = new BookingsController(db, bookingService, new TestNotificationService(), new TenantSettingsService(db));
        controller.ControllerContext = BuildContext(tenantId, memberUserId);
        return controller;
    }

    private static ControllerContext BuildContext(Guid tenantId, Guid memberUserId)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", memberUserId.ToString()),
            new(ClaimTypes.Role, "MEMBER")
        };
        var identity = new ClaimsIdentity(claims, "test");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private class TestNotificationService : INotificationService
    {
        public Task SendAsync(Guid tenantId, string toEmail, string type, object payload) => Task.CompletedTask;
    }
}
