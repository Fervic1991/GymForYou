using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreateClassRequest(
    [Required] string Title,
    string Description,
    [Required] Guid TrainerUserId,
    [Range(1, 500)] int Capacity,
    [Required] string RecurrenceRule,
    [Range(0, 6)] int WeeklyDayOfWeek,
    [Required, RegularExpression("^([01]\\d|2[0-3]):[0-5]\\d$")] string StartTimeUtc,
    [Range(15, 300)] int DurationMinutes,
    [Range(1, 50)] int MaxWeeklyBookingsPerMember
);

public record UpdateClassScheduleRequest(
    [Required] string Title,
    string Description,
    [Required] Guid TrainerUserId,
    [Range(1, 500)] int Capacity,
    [Range(0, 6)] int WeeklyDayOfWeek,
    [Required, RegularExpression("^([01]\\d|2[0-3]):[0-5]\\d$")] string StartTimeUtc,
    [Range(15, 300)] int DurationMinutes,
    [Range(1, 50)] int MaxWeeklyBookingsPerMember
);

public record CreateSessionRequest([Required] Guid GymClassId, [Required] DateTime StartAtUtc, [Required] DateTime EndAtUtc, [Range(0, 500)] int CapacityOverride);
public record CreateBookingRequest([Required] Guid SessionId, [Required] Guid MemberUserId);
public record SetSessionExceptionRequest([Required] Guid SessionId, bool Cancelled, DateTime? RescheduledStartAtUtc, DateTime? RescheduledEndAtUtc, Guid? TrainerOverrideUserId, string? Reason);
public record MarkBookingStatusRequest([Required] BookingStatus Status);
