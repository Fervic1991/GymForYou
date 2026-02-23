using GymForYou.Api.Data;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("dashboard")]
[Authorize(Roles = "OWNER,MANAGER,TRAINER")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis()
    {
        var now = DateTime.UtcNow;
        var startMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startTodayUtc = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var startWeek = startTodayUtc.AddDays(-(int)now.DayOfWeek);

        var activeMembers = await _db.MemberProfiles.CountAsync(x => x.Status == MemberStatus.ACTIVE);
        var revenueMonth = await _db.Payments.Where(x => x.CreatedAtUtc >= startMonth && x.Status == "paid").SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var weekCheckIns = await _db.CheckIns.CountAsync(x => x.CheckInAtUtc >= startWeek);
        var expiringMembers = await _db.MemberSubscriptions
            .Where(x => x.Status == SubscriptionStatus.ACTIVE
                        && x.EndsAtUtc.HasValue
                        && x.EndsAtUtc.Value >= now
                        && x.EndsAtUtc.Value <= now.AddDays(7))
            .Select(x => x.MemberUserId)
            .Distinct()
            .CountAsync();

        var sessionStats = await _db.ClassSessions
            .Select(s => new
            {
                Capacity = s.CapacityOverride > 0 ? s.CapacityOverride : _db.GymClasses.Where(c => c.Id == s.GymClassId).Select(c => c.Capacity).FirstOrDefault(),
                Booked = _db.Bookings.Count(b => b.SessionId == s.Id && b.Status == BookingStatus.BOOKED)
            }).ToListAsync();

        var fillRate = sessionStats.Count == 0 ? 0 : sessionStats.Average(x => x.Capacity == 0 ? 0 : (double)x.Booked / x.Capacity);

        return Ok(new
        {
            activeMembers,
            revenueMonth,
            weekCheckIns,
            fillRate,
            expiringMembers
        });
    }
}
