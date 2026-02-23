using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task Should_put_user_on_waitlist_when_capacity_reached()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 3, BookingBlockDays = 7, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var member1 = new User { TenantId = tenant, Email = "m1@g.com", FullName = "M1", PasswordHash = "x", Role = UserRole.MEMBER };
        var member2 = new User { TenantId = tenant, Email = "m2@g.com", FullName = "M2", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(trainer, member1, member2);
        db.MemberProfiles.AddRange(new MemberProfile { TenantId = tenant, UserId = member1.Id, CheckInCode = "A" }, new MemberProfile { TenantId = tenant, UserId = member2.Id, CheckInCode = "B" });
        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 1, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        db.GymClasses.Add(cls);
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var settingsService = new TenantSettingsService(db);
        var sut = new BookingService(db, settingsService);
        (await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, member1.Id))).Status.Should().Be(BookingStatus.BOOKED);
        (await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, member2.Id))).Status.Should().Be(BookingStatus.WAITLISTED);
    }

    [Fact]
    public async Task Cancel_one_booked_should_promote_first_waitlisted()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 99, BookingBlockDays = 7, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var m1 = new User { TenantId = tenant, Email = "m1@g.com", FullName = "M1", PasswordHash = "x", Role = UserRole.MEMBER };
        var m2 = new User { TenantId = tenant, Email = "m2@g.com", FullName = "M2", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(trainer, m1, m2);
        db.MemberProfiles.AddRange(new MemberProfile { TenantId = tenant, UserId = m1.Id, CheckInCode = "A" }, new MemberProfile { TenantId = tenant, UserId = m2.Id, CheckInCode = "B" });

        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 1, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.GymClasses.Add(cls); db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var sut = new BookingService(db, new TenantSettingsService(db));
        var b1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, m1.Id));
        var w1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, m2.Id));
        w1.Status.Should().Be(BookingStatus.WAITLISTED);

        var updated = await sut.MarkStatusAsync(b1.Id, BookingStatus.CANCELED);
        updated.PromotedBookings.Should().ContainSingle();
        updated.PromotedBookings[0].MemberUserId.Should().Be(m2.Id);
        updated.PromotedBookings[0].PromotedAtUtc.Should().NotBeNull();

        var promoted = await db.Bookings.FirstAsync(x => x.Id == w1.Id);
        promoted.Status.Should().Be(BookingStatus.BOOKED);
        promoted.PromotedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_two_booked_should_promote_two_waitlisted()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 99, BookingBlockDays = 7, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var users = Enumerable.Range(1, 4).Select(i => new User { TenantId = tenant, Email = $"m{i}@g.com", FullName = $"M{i}", PasswordHash = "x", Role = UserRole.MEMBER }).ToList();
        db.Users.Add(trainer);
        db.Users.AddRange(users);
        db.MemberProfiles.AddRange(users.Select((u, i) => new MemberProfile { TenantId = tenant, UserId = u.Id, CheckInCode = $"C{i}" }));

        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 2, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.GymClasses.Add(cls); db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var sut = new BookingService(db, new TenantSettingsService(db));
        var b1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[0].Id));
        var b2 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[1].Id));
        var w1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[2].Id));
        var w2 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[3].Id));

        (await sut.MarkStatusAsync(b1.Id, BookingStatus.CANCELED)).PromotedBookings.Should().ContainSingle();
        (await sut.MarkStatusAsync(b2.Id, BookingStatus.NO_SHOW)).PromotedBookings.Should().ContainSingle();

        var bookedCount = await db.Bookings.CountAsync(x => x.SessionId == session.Id && x.Status == BookingStatus.BOOKED);
        bookedCount.Should().Be(2);
        var promotedIds = await db.Bookings.Where(x => x.Status == BookingStatus.BOOKED && x.SessionId == session.Id).Select(x => x.MemberUserId).ToListAsync();
        promotedIds.Should().Contain(users[2].Id);
        promotedIds.Should().Contain(users[3].Id);
    }

    [Fact]
    public async Task No_promotion_when_waitlist_empty()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 99, BookingBlockDays = 7, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var m1 = new User { TenantId = tenant, Email = "m1@g.com", FullName = "M1", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(trainer, m1);
        db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = m1.Id, CheckInCode = "A" });

        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 1, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.GymClasses.Add(cls); db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var sut = new BookingService(db, new TenantSettingsService(db));
        var b1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, m1.Id));

        var result = await sut.MarkStatusAsync(b1.Id, BookingStatus.CANCELED);
        result.PromotedBookings.Should().BeEmpty();
    }

    [Fact]
    public async Task Promotion_should_never_exceed_capacity()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 99, BookingBlockDays = 7, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var users = Enumerable.Range(1, 5).Select(i => new User { TenantId = tenant, Email = $"m{i}@g.com", FullName = $"M{i}", PasswordHash = "x", Role = UserRole.MEMBER }).ToList();
        db.Users.Add(trainer);
        db.Users.AddRange(users);
        db.MemberProfiles.AddRange(users.Select((u, i) => new MemberProfile { TenantId = tenant, UserId = u.Id, CheckInCode = $"P{i}" }));

        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 2, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.GymClasses.Add(cls); db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var sut = new BookingService(db, new TenantSettingsService(db));
        var b1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[0].Id));
        var b2 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[1].Id));
        await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[2].Id));
        await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[3].Id));
        await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, users[4].Id));

        await sut.MarkStatusAsync(b1.Id, BookingStatus.CANCELED);
        await sut.MarkStatusAsync(b2.Id, BookingStatus.NO_SHOW);

        var bookedCount = await db.Bookings.CountAsync(x => x.SessionId == session.Id && x.Status == BookingStatus.BOOKED);
        bookedCount.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public async Task Quasi_concurrent_cancellations_should_not_exceed_capacity()
    {
        var tenant = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var optionsA = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var optionsB = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var setupDb = new AppDbContext(optionsA, new TenantProvider { TenantId = tenant });

        setupDb.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 99, BookingBlockDays = 7, CancelCutoffHours = 6 });
        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var users = Enumerable.Range(1, 6).Select(i => new User { TenantId = tenant, Email = $"m{i}@g.com", FullName = $"M{i}", PasswordHash = "x", Role = UserRole.MEMBER }).ToList();
        setupDb.Users.Add(trainer);
        setupDb.Users.AddRange(users);
        setupDb.MemberProfiles.AddRange(users.Select((u, i) => new MemberProfile { TenantId = tenant, UserId = u.Id, CheckInCode = $"Q{i}" }));

        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 2, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        setupDb.GymClasses.Add(cls); setupDb.ClassSessions.Add(session);
        await setupDb.SaveChangesAsync();

        var setupSvc = new BookingService(setupDb, new TenantSettingsService(setupDb));
        var b1 = await setupSvc.CreateBookingAsync(new CreateBookingRequest(session.Id, users[0].Id));
        var b2 = await setupSvc.CreateBookingAsync(new CreateBookingRequest(session.Id, users[1].Id));
        await setupSvc.CreateBookingAsync(new CreateBookingRequest(session.Id, users[2].Id));
        await setupSvc.CreateBookingAsync(new CreateBookingRequest(session.Id, users[3].Id));

        var dbA = new AppDbContext(optionsA, new TenantProvider { TenantId = tenant });
        var dbB = new AppDbContext(optionsB, new TenantProvider { TenantId = tenant });
        var svcA = new BookingService(dbA, new TenantSettingsService(dbA));
        var svcB = new BookingService(dbB, new TenantSettingsService(dbB));

        var t1 = Task.Run(() => svcA.MarkStatusAsync(b1.Id, BookingStatus.CANCELED));
        var t2 = Task.Run(() => svcB.MarkStatusAsync(b2.Id, BookingStatus.NO_SHOW));
        await Task.WhenAll(t1, t2);

        var verifyDb = new AppDbContext(optionsA, new TenantProvider { TenantId = tenant });
        var bookedCount = await verifyDb.Bookings.CountAsync(x => x.SessionId == session.Id && x.Status == BookingStatus.BOOKED);
        var waitlistCount = await verifyDb.Bookings.CountAsync(x => x.SessionId == session.Id && x.Status == BookingStatus.WAITLISTED);
        bookedCount.Should().BeLessOrEqualTo(2);
        waitlistCount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Should_block_member_after_too_many_noshows_or_late_cancels()
    {
        var tenant = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        db.TenantSettings.Add(new TenantSettings { TenantId = tenant, WeeklyBookingLimit = 10, MaxNoShows30d = 2, BookingBlockDays = 5, CancelCutoffHours = 6 });

        var trainer = new User { TenantId = tenant, Email = "t@g.com", FullName = "T", PasswordHash = "x", Role = UserRole.TRAINER };
        var member = new User { TenantId = tenant, Email = "m@g.com", FullName = "M", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(trainer, member);
        db.MemberProfiles.Add(new MemberProfile { TenantId = tenant, UserId = member.Id, CheckInCode = "CODE" });
        var cls = new GymClass { TenantId = tenant, Title = "Yoga", TrainerUserId = trainer.Id, Capacity = 10, RecurrenceRule = "FREQ=WEEKLY", MaxWeeklyBookingsPerMember = 10 };
        db.GymClasses.Add(cls);
        var session = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(1), EndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        var settingsService = new TenantSettingsService(db);
        var sut = new BookingService(db, settingsService);
        var b1 = await sut.CreateBookingAsync(new CreateBookingRequest(session.Id, member.Id));
        var session2 = new ClassSession { TenantId = tenant, GymClassId = cls.Id, StartAtUtc = DateTime.UtcNow.AddDays(2), EndAtUtc = DateTime.UtcNow.AddDays(2).AddHours(1) };
        db.ClassSessions.Add(session2);
        await db.SaveChangesAsync();
        var b2 = await sut.CreateBookingAsync(new CreateBookingRequest(session2.Id, member.Id));

        await sut.MarkStatusAsync(b1.Id, BookingStatus.NO_SHOW);
        await sut.MarkStatusAsync(b2.Id, BookingStatus.LATE_CANCEL);

        var profile = await db.MemberProfiles.FirstAsync(x => x.UserId == member.Id);
        profile.BookingBlockedUntilUtc.Should().NotBeNull();
    }
}
