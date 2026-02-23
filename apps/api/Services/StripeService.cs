using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace GymForYou.Api.Services;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(StartCheckoutRequest request, Guid tenantId);
    Event VerifyAndConstructEvent(string payload, string signatureHeader);
    Task HandleWebhookAsync(Event stripeEvent);
}

public class StripeService : IStripeService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<StripeService> _logger;

    public StripeService(IConfiguration config, AppDbContext db, ILogger<StripeService> logger)
    {
        _config = config;
        _db = db;
        _logger = logger;
        StripeConfiguration.ApiKey = _config["STRIPE_SECRET_KEY"];
    }

    public async Task<string> CreateCheckoutSessionAsync(StartCheckoutRequest request, Guid tenantId)
    {
        var member = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.MemberUserId && x.Role == UserRole.MEMBER)
            ?? throw new InvalidOperationException("Member not found");

        var plan = await _db.MembershipPlans.FirstOrDefaultAsync(x => x.Id == request.PlanId)
            ?? throw new InvalidOperationException("Plan not found");

        if (string.IsNullOrWhiteSpace(plan.StripePriceId))
            throw new InvalidOperationException("StripePriceId missing on plan");

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            CustomerEmail = member.Email,
            Metadata = new Dictionary<string, string>
            {
                ["TenantId"] = tenantId.ToString(),
                ["MemberId"] = member.Id.ToString(),
                ["PlanId"] = plan.Id.ToString(),
                ["tenantId"] = tenantId.ToString(), // legacy fallback
                ["memberUserId"] = member.Id.ToString(), // legacy fallback
                ["planId"] = plan.Id.ToString() // legacy fallback
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = plan.StripePriceId, Quantity = 1 }
            }
        });

        return session.Url ?? string.Empty;
    }

    public Event VerifyAndConstructEvent(string payload, string signatureHeader)
    {
        return EventUtility.ConstructEvent(payload, signatureHeader, _config["STRIPE_WEBHOOK_SECRET"]);
    }

    public async Task HandleWebhookAsync(Event stripeEvent)
    {
        var webhookLog = await TryRegisterWebhookEventAsync(stripeEvent);
        if (webhookLog is null)
            return;

        string outcome;
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                outcome = await HandleCheckoutCompletedAsync(stripeEvent.Data.Object as Session);
                break;
            case "invoice.payment_succeeded":
                outcome = await HandleInvoicePaidAsync(stripeEvent.Data.Object as Invoice);
                break;
            case "invoice.payment_failed":
                outcome = await HandleInvoiceFailedAsync(stripeEvent.Data.Object as Invoice);
                break;
            case "customer.subscription.deleted":
                outcome = await HandleSubscriptionDeletedAsync(stripeEvent.Data.Object as Subscription);
                break;
            default:
                _logger.LogInformation("Stripe event ignored: {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);
                outcome = "skipped_ignored_event";
                break;
        }

        webhookLog.Outcome = outcome;
        await _db.SaveChangesAsync();
    }

    private async Task<WebhookEventLog?> TryRegisterWebhookEventAsync(Event stripeEvent)
    {
        if (await _db.WebhookEventLogs.AsNoTracking()
            .AnyAsync(x => x.Provider == "stripe" && x.StripeEventId == stripeEvent.Id))
            return null;

        var log = new WebhookEventLog
        {
            Provider = "stripe",
            StripeEventId = stripeEvent.Id ?? string.Empty,
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type ?? string.Empty,
            Outcome = "processing"
        };
        _db.WebhookEventLogs.Add(log);

        try
        {
            await _db.SaveChangesAsync();
            return log;
        }
        catch (DbUpdateException ex)
        {
            // Handles webhook races: duplicate event should still respond 200 and not be reprocessed.
            if (await _db.WebhookEventLogs.AsNoTracking()
                .AnyAsync(x => x.Provider == "stripe" && x.StripeEventId == stripeEvent.Id))
            {
                _logger.LogInformation(ex, "Duplicate Stripe webhook ignored: {EventId}", stripeEvent.Id);
                return null;
            }

            throw;
        }
    }

    private async Task<string> HandleCheckoutCompletedAsync(Session? session)
    {
        if (session?.Metadata is null)
        {
            _logger.LogWarning("Stripe checkout completed missing metadata. SessionId={SessionId}", session?.Id);
            return "skipped_missing_metadata";
        }

        if (!TryReadMetadataIds(session.Metadata, out var tenantId, out var memberId, out var planId))
        {
            _logger.LogWarning("Stripe checkout completed invalid metadata. SessionId={SessionId}", session.Id);
            return "skipped_invalid_metadata";
        }

        var member = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == memberId && x.Role == UserRole.MEMBER);
        var plan = await _db.MembershipPlans.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == planId);
        if (member is null || plan is null || member.TenantId != tenantId || plan.TenantId != tenantId)
        {
            _logger.LogWarning("Stripe checkout metadata tenant mismatch. SessionId={SessionId} TenantId={TenantId} MemberId={MemberId} PlanId={PlanId}",
                session.Id, tenantId, memberId, planId);
            return "skipped_mismatch";
        }

        var existing = await _db.MemberSubscriptions.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.MemberUserId == memberId && x.PlanId == planId)
            .Where(x => x.StripeSubscriptionId == session.SubscriptionId || x.Status == SubscriptionStatus.ACTIVE)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            existing = new MemberSubscription
            {
                TenantId = tenantId,
                MemberUserId = memberId,
                PlanId = planId,
                Status = SubscriptionStatus.ACTIVE,
                StripeCustomerId = session.CustomerId,
                StripeSubscriptionId = session.SubscriptionId,
                IsManual = false
            };
            _db.MemberSubscriptions.Add(existing);
        }
        else
        {
            existing.Status = SubscriptionStatus.ACTIVE;
            existing.StripeCustomerId = session.CustomerId;
            existing.StripeSubscriptionId = session.SubscriptionId;
            existing.IsManual = false;
        }

        await _db.SaveChangesAsync();
        return "applied_checkout_completed";
    }

    private async Task<string> HandleInvoicePaidAsync(Invoice? invoice)
    {
        if (invoice is null || string.IsNullOrWhiteSpace(invoice.SubscriptionId))
        {
            _logger.LogWarning("Stripe invoice.payment_succeeded missing subscription id. InvoiceId={InvoiceId}", invoice?.Id);
            return "skipped_invalid_invoice";
        }

        var sub = await _db.MemberSubscriptions.IgnoreQueryFilters()
            .Where(x => x.StripeSubscriptionId == invoice.SubscriptionId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync();
        if (sub is null)
        {
            _logger.LogWarning("Stripe invoice.payment_succeeded unknown subscription id {StripeSubscriptionId}", invoice.SubscriptionId);
            return "skipped_subscription_not_found";
        }

        sub.Status = SubscriptionStatus.ACTIVE;

        var alreadyExists = await _db.Payments.IgnoreQueryFilters().AnyAsync(x =>
            (!string.IsNullOrWhiteSpace(invoice.Id) && x.StripeInvoiceId == invoice.Id) ||
            (!string.IsNullOrWhiteSpace(invoice.PaymentIntentId) && x.StripePaymentIntentId == invoice.PaymentIntentId));
        if (!alreadyExists)
        {
            _db.Payments.Add(new Payment
            {
                TenantId = sub.TenantId,
                MemberUserId = sub.MemberUserId,
                SubscriptionId = sub.Id,
                Amount = (invoice.AmountPaid > 0 ? invoice.AmountPaid : invoice.AmountDue) / 100m,
                Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "eur" : invoice.Currency.ToLowerInvariant(),
                Status = "paid",
                Method = GymForYou.Api.Models.PaymentMethod.STRIPE,
                StripeInvoiceId = invoice.Id,
                StripePaymentIntentId = invoice.PaymentIntentId
            });
        }

        await _db.SaveChangesAsync();
        return "applied_invoice_paid";
    }

    private async Task<string> HandleInvoiceFailedAsync(Invoice? invoice)
    {
        if (invoice is null || string.IsNullOrWhiteSpace(invoice.SubscriptionId))
        {
            _logger.LogWarning("Stripe invoice.payment_failed missing subscription id. InvoiceId={InvoiceId}", invoice?.Id);
            return "skipped_invalid_invoice";
        }

        var sub = await _db.MemberSubscriptions.IgnoreQueryFilters()
            .Where(x => x.StripeSubscriptionId == invoice.SubscriptionId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync();
        if (sub is null)
        {
            _logger.LogWarning("Stripe invoice.payment_failed unknown subscription id {StripeSubscriptionId}", invoice.SubscriptionId);
            return "skipped_subscription_not_found";
        }

        sub.Status = SubscriptionStatus.PAST_DUE;

        var alreadyExists = await _db.Payments.IgnoreQueryFilters().AnyAsync(x =>
            (!string.IsNullOrWhiteSpace(invoice.Id) && x.StripeInvoiceId == invoice.Id) ||
            (!string.IsNullOrWhiteSpace(invoice.PaymentIntentId) && x.StripePaymentIntentId == invoice.PaymentIntentId));
        if (!alreadyExists)
        {
            _db.Payments.Add(new Payment
            {
                TenantId = sub.TenantId,
                MemberUserId = sub.MemberUserId,
                SubscriptionId = sub.Id,
                Amount = invoice.AmountDue / 100m,
                Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "eur" : invoice.Currency.ToLowerInvariant(),
                Status = "failed",
                Method = GymForYou.Api.Models.PaymentMethod.STRIPE,
                StripeInvoiceId = invoice.Id,
                StripePaymentIntentId = invoice.PaymentIntentId
            });
        }

        await _db.SaveChangesAsync();
        return "applied_invoice_failed";
    }

    private async Task<string> HandleSubscriptionDeletedAsync(Subscription? subscription)
    {
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.Id))
        {
            _logger.LogWarning("Stripe customer.subscription.deleted missing id");
            return "skipped_invalid_subscription";
        }

        var subscriptions = await _db.MemberSubscriptions.IgnoreQueryFilters()
            .Where(x => x.StripeSubscriptionId == subscription.Id && x.Status != SubscriptionStatus.CANCELED)
            .ToListAsync();
        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("Stripe customer.subscription.deleted no active local rows. StripeSubscriptionId={StripeSubscriptionId}", subscription.Id);
            return "skipped_subscription_not_found";
        }

        foreach (var sub in subscriptions)
        {
            sub.Status = SubscriptionStatus.CANCELED;
            sub.EndsAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return "applied_subscription_deleted";
    }

    private static bool TryReadMetadataIds(Dictionary<string, string> metadata, out Guid tenantId, out Guid memberId, out Guid planId)
    {
        tenantId = Guid.Empty;
        memberId = Guid.Empty;
        planId = Guid.Empty;

        var tenantRaw = GetMetadata(metadata, "TenantId", "tenantId");
        var memberRaw = GetMetadata(metadata, "MemberId", "memberUserId");
        var planRaw = GetMetadata(metadata, "PlanId", "planId");

        return Guid.TryParse(tenantRaw, out tenantId) &&
               Guid.TryParse(memberRaw, out memberId) &&
               Guid.TryParse(planRaw, out planId);
    }

    private static string? GetMetadata(Dictionary<string, string> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
