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

public class MemberMyBookingsTests
{
    [Fact]
    public async Task Member_me_endpoint_returns_only_own_future_bookings()
    {
        var tenant = Guid.NewGuid();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, CancelCutoffHours = 6, WeeklyBookingLimit = 8, MaxNoShows30d = 3, BookingBlockDays = 7 });
        var trainer = new User { TenantId = tenant, Email = "trainer@g.com", FullName = "Trainer", PasswordHash = "x", Role = UserRole.TRAINER };
        var meUser = new User { TenantId = tenant, Id = me, Email = "me@g.com", FullName = "Me", PasswordHash = "x", Role = UserRole.MEMBER };
        var otherUser = new User { TenantId = tenant, Id = other, Email = "other@g.com", FullName = "Other", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(trainer, meUser, otherUser);
        db.MemberProfiles.AddRange(
            new MemberProfile { TenantId = tenant, UserId = me, CheckInCode = "ME" },
            new MemberProfile { TenantId = tenant, UserId = other, CheckInCode = "OT" }
        );

        var gymClass = new GymClass { TenantId = tenant, Title = "Yoga", Description = "", TrainerUserId = trainer.Id, Capacity = 10, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        db.GymClasses.Add(gymClass);
        await db.SaveChangesAsync();

        var futureSession = new ClassSession { TenantId = tenant, GymClassId = gymClass.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var pastSession = new ClassSession { TenantId = tenant, GymClassId = gymClass.Id, StartAtUtc = DateTime.UtcNow.AddDays(-1), EndAtUtc = DateTime.UtcNow.AddDays(-1).AddHours(1) };
        db.ClassSessions.AddRange(futureSession, pastSession);
        await db.SaveChangesAsync();

        db.Bookings.AddRange(
            new Booking { TenantId = tenant, SessionId = futureSession.Id, MemberUserId = me, Status = BookingStatus.BOOKED },
            new Booking { TenantId = tenant, SessionId = futureSession.Id, MemberUserId = other, Status = BookingStatus.WAITLISTED },
            new Booking { TenantId = tenant, SessionId = pastSession.Id, MemberUserId = me, Status = BookingStatus.BOOKED }
        );
        await db.SaveChangesAsync();

        var controller = new BookingsController(db, new BookingService(db, new TenantSettingsService(db)), new NoopNotificationService(), new TenantSettingsService(db))
        {
            ControllerContext = BuildContext(tenant, me)
        };

        var result = await controller.MyBookings();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();

        rows.Should().HaveCount(1);
        rows[0].ToString().Should().Contain("BOOKED");
    }

    private static ControllerContext BuildContext(Guid tenantId, Guid userId)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, "MEMBER")
        };
        var identity = new ClaimsIdentity(claims, "test");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private class NoopNotificationService : INotificationService
    {
        public Task SendAsync(Guid tenantId, string toEmail, string type, object payload) => Task.CompletedTask;
    }
}
