using GymForYou.Api.Data;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("notifications")]
[Authorize(Roles = "OWNER,MANAGER,TRAINER")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IRenewalReminderService _renewalReminderService;

    public NotificationsController(AppDbContext db, INotificationService notificationService, IRenewalReminderService renewalReminderService)
    {
        _db = db;
        _notificationService = notificationService;
        _renewalReminderService = renewalReminderService;
    }

    [HttpPost("booking-reminders")]
    public async Task<IActionResult> SendReminders()
    {
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);
        var now = DateTime.UtcNow;
        var tomorrow = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var bookings = await _db.Bookings
            .Join(_db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b, s })
            .Where(x => x.b.Status == Models.BookingStatus.BOOKED && x.s.StartAtUtc.Date == tomorrow)
            .ToListAsync();

        foreach (var item in bookings)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == item.b.MemberUserId);
            if (user is not null)
                await _notificationService.SendAsync(tenantId, user.Email, "booking_reminder", new { item.b.Id, item.s.StartAtUtc });
        }

        return Ok(new { sent = bookings.Count });
    }

    [HttpPost("renewal-reminders")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> SendRenewalReminders()
    {
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);
        var suspended = await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId && x.IsSuspended);
        if (suspended)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Tenant suspended",
                Detail = "Tenant suspended",
                Status = StatusCodes.Status403Forbidden,
                Instance = HttpContext.Request.Path
            });
        }

        var sent = await _renewalReminderService.SendForTenantAsync(tenantId);
        return Ok(new { sent });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs() => Ok(await _db.NotificationLogs.OrderByDescending(x => x.SentAtUtc).Take(100).ToListAsync());
}
