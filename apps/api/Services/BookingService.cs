using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace GymForYou.Api.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(CreateBookingRequest request);
    Task<BookingUpdateResult> MarkStatusAsync(Guid bookingId, BookingStatus status);
}

public record BookingUpdateResult(Booking UpdatedBooking, IReadOnlyList<Booking> PromotedBookings);

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly ITenantSettingsService _settingsService;

    public BookingService(AppDbContext db, ITenantSettingsService settingsService)
    {
        _db = db;
        _settingsService = settingsService;
    }

    public async Task<Booking> CreateBookingAsync(CreateBookingRequest request)
    {
        var session = await _db.ClassSessions.FirstOrDefaultAsync(x => x.Id == request.SessionId)
            ?? throw new InvalidOperationException("Session not found");

        var exception = await _db.SessionExceptions.FirstOrDefaultAsync(x => x.SessionId == session.Id);
        if (exception?.Cancelled == true)
            throw new InvalidOperationException("Session cancelled");

        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.UserId == request.MemberUserId)
            ?? throw new InvalidOperationException("Member not found");

        if (profile.BookingBlockedUntilUtc.HasValue && profile.BookingBlockedUntilUtc > DateTime.UtcNow)
            throw new InvalidOperationException($"Booking blocked until {profile.BookingBlockedUntilUtc:O}");

        var settings = await _settingsService.GetOrCreateAsync();
        var gymClass = await _db.GymClasses.FirstOrDefaultAsync(x => x.Id == session.GymClassId)
            ?? throw new InvalidOperationException("Class not found");

        var startWeek = DateTime.SpecifyKind(session.StartAtUtc.Date, DateTimeKind.Utc).AddDays(-(int)session.StartAtUtc.DayOfWeek);
        var endWeek = startWeek.AddDays(7);

        var weeklyCount = await _db.Bookings
            .Where(x => x.MemberUserId == request.MemberUserId && x.Status == BookingStatus.BOOKED)
            .Join(_db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b, s })
            .CountAsync(x => x.s.StartAtUtc >= startWeek && x.s.StartAtUtc < endWeek);

        var effectiveWeeklyLimit = Math.Min(settings.WeeklyBookingLimit, gymClass.MaxWeeklyBookingsPerMember);
        if (weeklyCount >= effectiveWeeklyLimit)
            throw new InvalidOperationException("Weekly booking limit reached");

        var capacity = session.CapacityOverride > 0 ? session.CapacityOverride : gymClass.Capacity;
        var booked = await _db.Bookings.CountAsync(x => x.SessionId == request.SessionId && x.Status == BookingStatus.BOOKED);

        var booking = new Booking
        {
            SessionId = request.SessionId,
            MemberUserId = request.MemberUserId,
            Status = booked >= capacity ? BookingStatus.WAITLISTED : BookingStatus.BOOKED
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<BookingUpdateResult> MarkStatusAsync(Guid bookingId, BookingStatus status)
    {
        IDbContextTransaction? tx = null;
        if (_db.Database.IsRelational())
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId)
                ?? throw new InvalidOperationException("Booking not found");

            if (status is not (BookingStatus.NO_SHOW or BookingStatus.LATE_CANCEL or BookingStatus.CANCELED or BookingStatus.BOOKED or BookingStatus.WAITLISTED))
                throw new InvalidOperationException("Unsupported status");

            var previousStatus = booking.Status;
            booking.Status = status;
            if (status is BookingStatus.CANCELED or BookingStatus.LATE_CANCEL)
                booking.CanceledAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var promoted = new List<Booking>();
            var freesSpot = previousStatus == BookingStatus.BOOKED &&
                            (status == BookingStatus.CANCELED || status == BookingStatus.LATE_CANCEL || status == BookingStatus.NO_SHOW);
            if (freesSpot)
            {
                promoted = await PromoteWaitlistAsync(booking.SessionId);
            }

            await EnforceBlockIfNeededAsync(booking.MemberUserId);
            await _db.SaveChangesAsync();
            if (tx is not null)
                await tx.CommitAsync();

            return new BookingUpdateResult(booking, promoted);
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    private async Task<List<Booking>> PromoteWaitlistAsync(Guid sessionId)
    {
        var session = await _db.ClassSessions.FirstOrDefaultAsync(x => x.Id == sessionId)
            ?? throw new InvalidOperationException("Session not found");
        var gymClass = await _db.GymClasses.FirstOrDefaultAsync(x => x.Id == session.GymClassId)
            ?? throw new InvalidOperationException("Class not found");

        var capacity = session.CapacityOverride > 0 ? session.CapacityOverride : gymClass.Capacity;
        var bookedCount = await _db.Bookings.CountAsync(x => x.SessionId == sessionId && x.Status == BookingStatus.BOOKED);
        var freeSlots = Math.Max(0, capacity - bookedCount);
        if (freeSlots == 0)
            return new List<Booking>();

        var toPromote = await _db.Bookings
            .Where(x => x.SessionId == sessionId && x.Status == BookingStatus.WAITLISTED)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(freeSlots)
            .ToListAsync();

        var promotedAt = DateTime.UtcNow;
        foreach (var w in toPromote)
        {
            w.Status = BookingStatus.BOOKED;
            w.PromotedAtUtc = promotedAt;
        }

        if (toPromote.Count > 0)
            await _db.SaveChangesAsync();

        // guard: never exceed capacity
        var afterBookedCount = await _db.Bookings.CountAsync(x => x.SessionId == sessionId && x.Status == BookingStatus.BOOKED);
        if (afterBookedCount > capacity)
            throw new InvalidOperationException("Capacity exceeded during waitlist promotion");

        return toPromote;
    }

    private async Task EnforceBlockIfNeededAsync(Guid memberUserId)
    {
        var settings = await _settingsService.GetOrCreateAsync();
        var since = DateTime.UtcNow.AddDays(-30);

        var badCount = await _db.Bookings.CountAsync(x =>
            x.MemberUserId == memberUserId &&
            x.CreatedAtUtc >= since &&
            (x.Status == BookingStatus.NO_SHOW || x.Status == BookingStatus.LATE_CANCEL));

        if (badCount < settings.MaxNoShows30d)
            return;

        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.UserId == memberUserId);
        if (profile is null) return;

        var until = DateTime.UtcNow.AddDays(settings.BookingBlockDays);
        if (!profile.BookingBlockedUntilUtc.HasValue || profile.BookingBlockedUntilUtc < until)
            profile.BookingBlockedUntilUtc = until;
    }
}
