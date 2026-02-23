using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBookingService _bookingService;
    private readonly INotificationService _notifications;
    private readonly ITenantSettingsService _settingsService;

    public BookingsController(AppDbContext db, IBookingService bookingService, INotificationService notifications, ITenantSettingsService settingsService)
    {
        _db = db;
        _bookingService = bookingService;
        _notifications = notifications;
        _settingsService = settingsService;
    }

    [HttpGet]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Get() => Ok(await _db.Bookings.OrderByDescending(x => x.CreatedAtUtc).ToListAsync());

    [HttpGet("me")]
    [Authorize(Roles = "MEMBER")]
    public async Task<IActionResult> MyBookings([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var memberUserId = GetCurrentUserId();
        var start = from ?? DateTime.UtcNow;
        var end = to ?? DateTime.UtcNow.AddDays(90);

        var data = await _db.Bookings
            .Where(b => b.MemberUserId == memberUserId && (b.Status == BookingStatus.BOOKED || b.Status == BookingStatus.WAITLISTED))
            .Join(_db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b, s })
            .Join(_db.GymClasses, bs => bs.s.GymClassId, c => c.Id, (bs, c) => new
            {
                bookingId = bs.b.Id,
                bs.b.Status,
                bs.s.StartAtUtc,
                bs.s.EndAtUtc,
                classTitle = c.Title,
                classId = c.Id
            })
            .Where(x => x.StartAtUtc >= start && x.StartAtUtc <= end)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER,MEMBER")]
    public async Task<IActionResult> Create(CreateBookingRequest request)
    {
        var booking = await _bookingService.CreateBookingAsync(request);
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);

        var member = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.MemberUserId);
        if (member is not null)
            await _notifications.SendAsync(tenantId, member.Email, "booking_confirmation", new { booking.Id, booking.Status });

        return Ok(booking);
    }

    /// <summary>
    /// Aggiorna lo stato prenotazione. Se un BOOKED libera posto (CANCELED/LATE_CANCEL/NO_SHOW),
    /// il sistema promuove automaticamente il primo WAITLISTED della stessa sessione (CreatedAtUtc asc).
    /// </summary>
    [HttpPatch("{bookingId:guid}/status")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> MarkStatus(Guid bookingId, MarkBookingStatusRequest request)
    {
        var result = await _bookingService.MarkStatusAsync(bookingId, request.Status);
        await NotifyWaitlistPromotedAsync(result.PromotedBookings);
        return Ok(result.UpdatedBooking);
    }

    [HttpPatch("{bookingId:guid}/no-show")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> MarkNoShow(Guid bookingId)
    {
        var result = await _bookingService.MarkStatusAsync(bookingId, BookingStatus.NO_SHOW);
        await NotifyWaitlistPromotedAsync(result.PromotedBookings);
        return Ok(result.UpdatedBooking);
    }

    /// <summary>
    /// Cancella la prenotazione applicando la policy cutoff (CANCELED o LATE_CANCEL).
    /// Se viene liberato un posto, promuove automaticamente la waitlist in ordine FIFO.
    /// </summary>
    [HttpPatch("{bookingId:guid}/cancel")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER,MEMBER")]
    public async Task<IActionResult> Cancel(Guid bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId);
        if (booking is null) return NotFound();

        var isMember = User.IsInRole("MEMBER");
        var currentUserId = GetCurrentUserId();
        if (isMember && booking.MemberUserId != currentUserId)
            return Forbid();

        var settings = await _settingsService.GetOrCreateAsync();
        var session = await _db.ClassSessions.FirstOrDefaultAsync(x => x.Id == booking.SessionId);
        if (session is null) return NotFound("Session not found");

        var cutoff = session.StartAtUtc.AddHours(-settings.CancelCutoffHours);
        var status = DateTime.UtcNow > cutoff ? BookingStatus.LATE_CANCEL : BookingStatus.CANCELED;

        var result = await _bookingService.MarkStatusAsync(bookingId, status);
        var updated = result.UpdatedBooking;
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);
        var member = await _db.Users.FirstOrDefaultAsync(x => x.Id == updated.MemberUserId);
        if (member is not null)
        {
            await _notifications.SendAsync(tenantId, member.Email, "BookingCanceled", new { updated.Id, updated.Status, session.StartAtUtc });
        }

        await NotifyWaitlistPromotedAsync(result.PromotedBookings);

        return Ok(updated);
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId))
            throw new InvalidOperationException("User id claim missing");
        return userId;
    }

    private async Task NotifyWaitlistPromotedAsync(IReadOnlyList<Booking> promoted)
    {
        if (promoted.Count == 0) return;
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);

        foreach (var p in promoted)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == p.MemberUserId);
            if (user is null) continue;
            await _notifications.SendAsync(tenantId, user.Email, "WaitlistPromoted", new { p.Id, p.SessionId, p.CreatedAtUtc });
        }
    }
}
