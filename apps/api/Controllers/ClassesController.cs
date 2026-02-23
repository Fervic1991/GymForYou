using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("classes")]
[Authorize]
public class ClassesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClassesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER,MEMBER")]
    public async Task<IActionResult> Get() => Ok(await _db.GymClasses.OrderBy(x => x.Title).ToListAsync());

    [HttpPost]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Create(CreateClassRequest request)
    {
        var gymClass = new GymClass
        {
            Title = request.Title,
            Description = request.Description,
            TrainerUserId = request.TrainerUserId,
            Capacity = request.Capacity,
            RecurrenceRule = request.RecurrenceRule,
            WeeklyDayOfWeek = request.WeeklyDayOfWeek,
            StartTimeUtc = request.StartTimeUtc,
            DurationMinutes = request.DurationMinutes,
            IsActive = true,
            MaxWeeklyBookingsPerMember = request.MaxWeeklyBookingsPerMember
        };
        _db.GymClasses.Add(gymClass);
        await _db.SaveChangesAsync();
        await GenerateUpcomingSessionsAsync(gymClass, weeks: 24);
        return Ok(gymClass);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Update(Guid id, UpdateClassScheduleRequest request)
    {
        var gymClass = await _db.GymClasses.FirstOrDefaultAsync(x => x.Id == id);
        if (gymClass is null) return NotFound("Class not found");

        var now = DateTime.UtcNow;
        var futureSessions = await _db.ClassSessions
            .Where(x => x.GymClassId == gymClass.Id && x.StartAtUtc > now)
            .ToListAsync();
        var futureSessionIds = futureSessions.Select(x => x.Id).ToList();
        var bookedFutureSessionIds = await _db.Bookings
            .Where(x => futureSessionIds.Contains(x.SessionId) && x.Status == BookingStatus.BOOKED)
            .Select(x => x.SessionId)
            .Distinct()
            .ToListAsync();
        var toDelete = futureSessions.Where(x => !bookedFutureSessionIds.Contains(x.Id)).ToList();
        if (toDelete.Count > 0) _db.ClassSessions.RemoveRange(toDelete);

        gymClass.Title = request.Title;
        gymClass.Description = request.Description;
        gymClass.TrainerUserId = request.TrainerUserId;
        gymClass.Capacity = request.Capacity;
        gymClass.WeeklyDayOfWeek = request.WeeklyDayOfWeek;
        gymClass.StartTimeUtc = request.StartTimeUtc;
        gymClass.DurationMinutes = request.DurationMinutes;
        gymClass.MaxWeeklyBookingsPerMember = request.MaxWeeklyBookingsPerMember;
        gymClass.RecurrenceRule = $"FREQ=WEEKLY;BYDAY={ToByDay(request.WeeklyDayOfWeek)}";

        await _db.SaveChangesAsync();
        await GenerateUpcomingSessionsAsync(gymClass, weeks: 24);

        return Ok(gymClass);
    }

    [HttpPost("{id:guid}/discontinue")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Discontinue(Guid id)
    {
        var gymClass = await _db.GymClasses.FirstOrDefaultAsync(x => x.Id == id);
        if (gymClass is null) return NotFound("Class not found");

        gymClass.IsActive = false;

        var now = DateTime.UtcNow;
        var futureSessions = await _db.ClassSessions
            .Where(x => x.GymClassId == gymClass.Id && x.StartAtUtc > now)
            .ToListAsync();

        var sessionIds = futureSessions.Select(x => x.Id).ToList();
        var existingExceptions = await _db.SessionExceptions
            .Where(x => sessionIds.Contains(x.SessionId))
            .ToDictionaryAsync(x => x.SessionId, x => x);

        foreach (var s in futureSessions)
        {
            if (!existingExceptions.TryGetValue(s.Id, out var ex))
            {
                _db.SessionExceptions.Add(new SessionException
                {
                    SessionId = s.Id,
                    Cancelled = true,
                    Reason = "Class discontinued"
                });
            }
            else
            {
                ex.Cancelled = true;
                ex.Reason = "Class discontinued";
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { gymClass.Id, gymClass.IsActive, canceledSessions = futureSessions.Count });
    }

    [HttpGet("sessions")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER,MEMBER")]
    public async Task<IActionResult> Sessions([FromQuery] DateTime? weekStart = null)
    {
        var now = DateTime.UtcNow;
        var defaultStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-(int)now.DayOfWeek);
        var weekStartUtc = weekStart.HasValue ? DateTime.SpecifyKind(weekStart.Value, DateTimeKind.Utc) : defaultStart;
        var start = new DateTime(weekStartUtc.Year, weekStartUtc.Month, weekStartUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(7);

        var sessions = await _db.ClassSessions.Where(x => x.StartAtUtc >= start && x.StartAtUtc < end).OrderBy(x => x.StartAtUtc).ToListAsync();
        var exceptions = await _db.SessionExceptions.Where(x => sessions.Select(s => s.Id).Contains(x.SessionId)).ToListAsync();
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var bookedCounts = await _db.Bookings
            .Where(x => sessionIds.Contains(x.SessionId) && x.Status == BookingStatus.BOOKED)
            .GroupBy(x => x.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);

        return Ok(sessions.Select(s =>
        {
            var ex = exceptions.FirstOrDefault(x => x.SessionId == s.Id);
            return new
            {
                s.Id,
                s.GymClassId,
                s.StartAtUtc,
                s.EndAtUtc,
                s.CapacityOverride,
                BookedCount = bookedCounts.GetValueOrDefault(s.Id, 0),
                Exception = ex
            };
        }));
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request)
    {
        var session = new ClassSession
        {
            GymClassId = request.GymClassId,
            StartAtUtc = DateTime.SpecifyKind(request.StartAtUtc, DateTimeKind.Utc),
            EndAtUtc = DateTime.SpecifyKind(request.EndAtUtc, DateTimeKind.Utc),
            CapacityOverride = request.CapacityOverride
        };
        _db.ClassSessions.Add(session);
        await _db.SaveChangesAsync();
        return Ok(session);
    }

    [HttpPost("sessions/exceptions")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> SetException(SetSessionExceptionRequest request)
    {
        var session = await _db.ClassSessions.FirstOrDefaultAsync(x => x.Id == request.SessionId);
        if (session is null) return NotFound("Session not found");

        var ex = await _db.SessionExceptions.FirstOrDefaultAsync(x => x.SessionId == request.SessionId);
        if (ex is null)
        {
            ex = new SessionException { SessionId = request.SessionId };
            _db.SessionExceptions.Add(ex);
        }

        ex.Cancelled = request.Cancelled;
        ex.RescheduledStartAtUtc = request.RescheduledStartAtUtc.HasValue ? DateTime.SpecifyKind(request.RescheduledStartAtUtc.Value, DateTimeKind.Utc) : null;
        ex.RescheduledEndAtUtc = request.RescheduledEndAtUtc.HasValue ? DateTime.SpecifyKind(request.RescheduledEndAtUtc.Value, DateTimeKind.Utc) : null;
        ex.TrainerOverrideUserId = request.TrainerOverrideUserId;
        ex.Reason = request.Reason;

        await _db.SaveChangesAsync();
        return Ok(ex);
    }

    private async Task GenerateUpcomingSessionsAsync(GymClass gymClass, int weeks)
    {
        if (!gymClass.IsActive) return;

        var now = DateTime.UtcNow;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var dayDelta = ((gymClass.WeeklyDayOfWeek - (int)today.DayOfWeek) + 7) % 7;
        var firstDay = today.AddDays(dayDelta);

        var parts = gymClass.StartTimeUtc.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return;
        if (!int.TryParse(parts[0], out var hh) || !int.TryParse(parts[1], out var mm)) return;

        for (var i = 0; i < weeks; i++)
        {
            var start = firstDay.AddDays(i * 7).AddHours(hh).AddMinutes(mm);
            if (start <= now) continue;
            var end = start.AddMinutes(gymClass.DurationMinutes);

            var exists = await _db.ClassSessions.AnyAsync(x => x.GymClassId == gymClass.Id && x.StartAtUtc == start);
            if (exists) continue;

            _db.ClassSessions.Add(new ClassSession
            {
                GymClassId = gymClass.Id,
                StartAtUtc = start,
                EndAtUtc = end,
                CapacityOverride = 0
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string ToByDay(int day) => day switch
    {
        0 => "SU",
        1 => "MO",
        2 => "TU",
        3 => "WE",
        4 => "TH",
        5 => "FR",
        6 => "SA",
        _ => "MO"
    };
}
