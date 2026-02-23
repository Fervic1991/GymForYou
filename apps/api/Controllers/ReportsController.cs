using GymForYou.Api.Data;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("reports")]
[Authorize(Roles = "OWNER,MANAGER")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var now = DateTime.UtcNow;
        var startCurrent = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startPrev = startCurrent.AddMonths(-1);

        var activeCurrent = await _db.MemberProfiles.CountAsync(x => x.Status == MemberStatus.ACTIVE);
        var activePrev = await _db.MemberProfiles.CountAsync(x => x.Status == MemberStatus.ACTIVE && x.LastCheckInUtc < startCurrent);

        var days30 = now.AddDays(-30);
        var checkins = await _db.CheckIns.Where(x => x.CheckInAtUtc >= days30).CountAsync();
        var bookings = await _db.Bookings.Where(x => x.CreatedAtUtc >= days30 && x.Status == BookingStatus.BOOKED).CountAsync();
        var membersCount = await _db.MemberProfiles.CountAsync();

        var topClasses = await _db.Bookings
            .Where(x => x.Status == BookingStatus.BOOKED)
            .Join(_db.ClassSessions, b => b.SessionId, s => s.Id, (b, s) => new { b, s })
            .Join(_db.GymClasses, bs => bs.s.GymClassId, c => c.Id, (bs, c) => new { c.Title })
            .GroupBy(x => x.Title)
            .Select(g => new { title = g.Key, bookings = g.Count() })
            .OrderByDescending(x => x.bookings)
            .Take(5)
            .ToListAsync();

        return Ok(new
        {
            churn = new
            {
                activeCurrent,
                activePrevious = activePrev,
                delta = activeCurrent - activePrev
            },
            averageFrequency = new
            {
                checkinsPerMember30d = membersCount == 0 ? 0 : (double)checkins / membersCount,
                bookingsPerMember30d = membersCount == 0 ? 0 : (double)bookings / membersCount
            },
            topClasses
        });
    }
}
