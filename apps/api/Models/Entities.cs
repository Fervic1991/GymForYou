using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.Models;

public enum UserRole { PLATFORM_ADMIN, OWNER, MANAGER, TRAINER, MEMBER }
public enum MemberStatus { ACTIVE, SUSPENDED }
public enum SubscriptionStatus { ACTIVE, PAST_DUE, CANCELED, INCOMPLETE }
public enum BookingStatus { BOOKED, WAITLISTED, CANCELED, NO_SHOW, LATE_CANCEL }
public enum PaymentMethod { CASH, BANK_TRANSFER, POS, STRIPE }
public enum VideoProvider { YOUTUBE, VIMEO }

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(120)] public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    [MaxLength(20)] public string PrimaryColor { get; set; } = "#1d4ed8";
    [MaxLength(20)] public string SecondaryColor { get; set; } = "#0f172a";
    [MaxLength(2)] public string DefaultLocale { get; set; } = "it";
    [MaxLength(24)] public string? JoinCode { get; set; }
    [MaxLength(120)] public string? City { get; set; }
    [MaxLength(200)] public string? Address { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    public bool IsSuspended { get; set; }
    [MaxLength(20)] public string BillingStatus { get; set; } = "PAID";
    public DateTime? BillingValidUntilUtc { get; set; }
    public DateTime? BillingLastUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TenantSettings : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int CancelCutoffHours { get; set; } = 6;
    public int MaxNoShows30d { get; set; } = 3;
    public int WeeklyBookingLimit { get; set; } = 8;
    public int BookingBlockDays { get; set; } = 7;
}

public class User : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(180)] public string Email { get; set; } = string.Empty;
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RefreshToken : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(240)] public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public class StaffInvite : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(180)] public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    [MaxLength(240)] public string InviteToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool Accepted { get; set; }
}

public class MemberProfile : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.ACTIVE;
    public DateTime? LastCheckInUtc { get; set; }
    [MaxLength(80)] public string CheckInCode { get; set; } = string.Empty;
    public DateTime? CheckInCodeExpiresAtUtc { get; set; }
    public DateTime? BookingBlockedUntilUtc { get; set; }
}

public class MembershipPlan : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    [MaxLength(20)] public string Interval { get; set; } = "monthly";
    [MaxLength(120)] public string? StripePriceId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MemberSubscription : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MemberUserId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.INCOMPLETE;
    [MaxLength(120)] public string? StripeCustomerId { get; set; }
    [MaxLength(120)] public string? StripeSubscriptionId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAtUtc { get; set; }
    public bool IsManual { get; set; }
}

public class GymClass : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(120)] public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid TrainerUserId { get; set; }
    public int Capacity { get; set; }
    [MaxLength(120)] public string RecurrenceRule { get; set; } = "FREQ=WEEKLY;BYDAY=MO,WE,FR";
    public int WeeklyDayOfWeek { get; set; } = 1; // 0=Sunday..6=Saturday
    [MaxLength(5)] public string StartTimeUtc { get; set; } = "15:00"; // HH:mm
    public int DurationMinutes { get; set; } = 60;
    public bool IsActive { get; set; } = true;
    public int MaxWeeklyBookingsPerMember { get; set; } = 10;
}

public class ClassSession : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid GymClassId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public int CapacityOverride { get; set; }
}

public class SessionException : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public bool Cancelled { get; set; }
    public DateTime? RescheduledStartAtUtc { get; set; }
    public DateTime? RescheduledEndAtUtc { get; set; }
    public Guid? TrainerOverrideUserId { get; set; }
    [MaxLength(180)] public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Booking : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid MemberUserId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.BOOKED;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CanceledAtUtc { get; set; }
    public DateTime? PromotedAtUtc { get; set; }
}

public class CheckIn : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MemberUserId { get; set; }
    public DateTime CheckInAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(140)] public string Source { get; set; } = "manual";
}

public class ExerciseVideo : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(140)] public string Title { get; set; } = string.Empty;
    [MaxLength(80)] public string Category { get; set; } = "General";
    [MaxLength(2000)] public string VideoUrl { get; set; } = string.Empty;
    [MaxLength(2000)] public string? ThumbnailUrl { get; set; }
    public string Description { get; set; } = string.Empty;
    public VideoProvider Provider { get; set; } = VideoProvider.YOUTUBE;
    public int DurationSeconds { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class VideoProgress : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid VideoId { get; set; }
    public Guid MemberUserId { get; set; }
    public int WatchedSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime LastViewedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TenantJoinLink : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(40)] public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAtUtc { get; set; }
    public int? MaxUses { get; set; }
    public int UsesCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Payment : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MemberUserId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = "eur";
    [MaxLength(40)] public string Status { get; set; } = "pending";
    public PaymentMethod Method { get; set; } = PaymentMethod.STRIPE;
    [MaxLength(120)] public string? StripePaymentIntentId { get; set; }
    [MaxLength(120)] public string? StripeInvoiceId { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class NotificationLog : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(180)] public string ToEmail { get; set; } = string.Empty;
    [MaxLength(120)] public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

public class WebhookEventLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Provider { get; set; } = "stripe";
    [MaxLength(120)] public string StripeEventId { get; set; } = string.Empty;
    [MaxLength(120)] public string? EventId { get; set; } // legacy compatibility
    [MaxLength(120)] public string EventType { get; set; } = string.Empty;
    [MaxLength(120)] public string Outcome { get; set; } = "processed";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
