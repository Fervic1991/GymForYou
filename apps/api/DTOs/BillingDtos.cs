using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreatePlanRequest([Required] string Name, string Description, [Range(0, 9999)] decimal Price, [Required] string Interval, string? StripePriceId);
public record AssignPlanRequest([Required] Guid MemberUserId, [Required] Guid PlanId);
public record StartCheckoutRequest([Required] Guid MemberUserId, [Required] Guid PlanId, [Required] string SuccessUrl, [Required] string CancelUrl);

public record ManualPaymentRequest(
    [Required] Guid MemberUserId,
    Guid? SubscriptionId,
    [Range(0.01, 999999)] decimal Amount,
    string Currency,
    [Required] PaymentMethod Method,
    string? Notes,
    bool MarkAsPaid = true,
    int? RenewDays = null,
    Guid? PlanId = null);

public record ManualSubscriptionRequest(
    [Required] Guid MemberUserId,
    [Required] Guid PlanId,
    [Range(1, 3650)] int DurationDays,
    [Required] PaymentMethod PaymentMethod,
    [Range(0.01, 999999)] decimal Amount,
    string? Notes);
