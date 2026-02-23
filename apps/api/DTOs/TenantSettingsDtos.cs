using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record UpdateTenantSettingsRequest(
    [Range(0, 168)] int CancelCutoffHours,
    [Range(0, 20)] int MaxNoShows30d,
    [Range(1, 100)] int WeeklyBookingLimit,
    [Range(1, 60)] int BookingBlockDays);

public record TenantSettingsResponse(
    Guid TenantId,
    string TenantName,
    int CancelCutoffHours,
    int MaxNoShows30d,
    int WeeklyBookingLimit,
    int BookingBlockDays,
    string DefaultLocale,
    string PrimaryColor,
    string SecondaryColor
);
