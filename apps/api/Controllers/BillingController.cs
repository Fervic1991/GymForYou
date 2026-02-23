using System.Text;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStripeService _stripe;

    public BillingController(AppDbContext db, IStripeService stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    [HttpGet("plans")]
    [Authorize]
    public async Task<IActionResult> GetPlans() => Ok(await _db.MembershipPlans.ToListAsync());

    [HttpPost("plans")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> CreatePlan(CreatePlanRequest request)
    {
        var plan = new MembershipPlan
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Interval = request.Interval,
            StripePriceId = request.StripePriceId
        };
        _db.MembershipPlans.Add(plan);
        await _db.SaveChangesAsync();
        return Ok(plan);
    }

    [HttpPost("subscriptions/assign")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> Assign(AssignPlanRequest request)
    {
        var sub = new MemberSubscription
        {
            MemberUserId = request.MemberUserId,
            PlanId = request.PlanId,
            Status = SubscriptionStatus.ACTIVE
        };
        _db.MemberSubscriptions.Add(sub);
        await _db.SaveChangesAsync();
        return Ok(sub);
    }

    [HttpPost("subscriptions/manual")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> CreateManualSubscription(ManualSubscriptionRequest request)
    {
        var now = DateTime.UtcNow;
        var sub = new MemberSubscription
        {
            MemberUserId = request.MemberUserId,
            PlanId = request.PlanId,
            Status = SubscriptionStatus.ACTIVE,
            StartedAtUtc = now,
            EndsAtUtc = now.AddDays(request.DurationDays),
            IsManual = true
        };

        _db.MemberSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        _db.Payments.Add(new Payment
        {
            MemberUserId = request.MemberUserId,
            SubscriptionId = sub.Id,
            Amount = request.Amount,
            Currency = "eur",
            Method = request.PaymentMethod,
            Status = "paid",
            Notes = request.Notes
        });
        await _db.SaveChangesAsync();

        return Ok(sub);
    }

    [HttpGet("subscriptions")]
    [Authorize]
    public async Task<IActionResult> Subscriptions()
    {
        var q = _db.MemberSubscriptions.AsQueryable();
        if (User.IsInRole("MEMBER"))
        {
            var memberUserId = GetCurrentUserId();
            q = q.Where(x => x.MemberUserId == memberUserId);
        }

        return Ok(await q.OrderByDescending(x => x.StartedAtUtc).ToListAsync());
    }

    [HttpGet("me/subscriptions")]
    [Authorize(Roles = "MEMBER")]
    public async Task<IActionResult> MySubscriptions()
    {
        var memberUserId = GetCurrentUserId();
        var data = await _db.MemberSubscriptions
            .Where(x => x.MemberUserId == memberUserId)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("payments")]
    [Authorize]
    public async Task<IActionResult> Payments([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentMethod? method = null)
    {
        var q = _db.Payments.AsQueryable();
        if (User.IsInRole("MEMBER"))
        {
            var memberUserId = GetCurrentUserId();
            q = q.Where(x => x.MemberUserId == memberUserId);
        }

        if (from.HasValue) q = q.Where(x => x.CreatedAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.CreatedAtUtc <= to.Value);
        if (method.HasValue) q = q.Where(x => x.Method == method.Value);
        return Ok(await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [HttpGet("me/payments")]
    [Authorize(Roles = "MEMBER")]
    public async Task<IActionResult> MyPayments([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentMethod? method = null)
    {
        var memberUserId = GetCurrentUserId();
        var q = _db.Payments.Where(x => x.MemberUserId == memberUserId);
        if (from.HasValue) q = q.Where(x => x.CreatedAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.CreatedAtUtc <= to.Value);
        if (method.HasValue) q = q.Where(x => x.Method == method.Value);
        return Ok(await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [HttpGet("payments/export")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> ExportPaymentsCsv([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentMethod? method = null)
    {
        if (!User.IsInRole("OWNER") && !User.IsInRole("MANAGER"))
            return Forbid();

        var q = _db.Payments.AsQueryable();
        if (from.HasValue) q = q.Where(x => x.CreatedAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(x => x.CreatedAtUtc <= to.Value);
        if (method.HasValue) q = q.Where(x => x.Method == method.Value);

        var data = await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("id,memberUserId,subscriptionId,amount,currency,status,method,createdAtUtc,notes");
        foreach (var p in data)
        {
            sb.AppendLine($"{p.Id},{p.MemberUserId},{p.SubscriptionId},{p.Amount},{p.Currency},{p.Status},{p.Method},{p.CreatedAtUtc:O},\"{(p.Notes ?? string.Empty).Replace("\"", "\"\"")}\"");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"payments-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("payments/manual")]
    [Authorize(Roles = "OWNER,MANAGER")]
    public async Task<IActionResult> RegisterManualPayment(ManualPaymentRequest request)
    {
        Guid? subscriptionId = request.SubscriptionId;

        if (request.RenewDays.HasValue)
        {
            var sub = await _db.MemberSubscriptions
                .Where(x => x.MemberUserId == request.MemberUserId && x.Status == SubscriptionStatus.ACTIVE)
                .OrderByDescending(x => x.EndsAtUtc)
                .FirstOrDefaultAsync();

            if (sub is not null)
            {
                var start = sub.EndsAtUtc.HasValue && sub.EndsAtUtc.Value > DateTime.UtcNow ? sub.EndsAtUtc.Value : DateTime.UtcNow;
                sub.EndsAtUtc = start.AddDays(request.RenewDays.Value);
                subscriptionId = sub.Id;
            }
            else if (request.PlanId.HasValue)
            {
                var created = new MemberSubscription
                {
                    MemberUserId = request.MemberUserId,
                    PlanId = request.PlanId.Value,
                    Status = SubscriptionStatus.ACTIVE,
                    StartedAtUtc = DateTime.UtcNow,
                    EndsAtUtc = DateTime.UtcNow.AddDays(request.RenewDays.Value),
                    IsManual = true
                };
                _db.MemberSubscriptions.Add(created);
                await _db.SaveChangesAsync();
                subscriptionId = created.Id;
            }
        }

        var payment = new Payment
        {
            MemberUserId = request.MemberUserId,
            SubscriptionId = subscriptionId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "eur" : request.Currency.ToLowerInvariant(),
            Method = request.Method,
            Status = request.MarkAsPaid ? "paid" : "pending",
            Notes = request.Notes
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return Ok(payment);
    }

    [HttpPost("checkout")]
    [Authorize(Roles = "OWNER,MANAGER,MEMBER")]
    public async Task<IActionResult> Checkout(StartCheckoutRequest request)
    {
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);
        var tenantSuspended = await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId && x.IsSuspended);
        if (tenantSuspended)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Tenant suspended",
                Detail = "Tenant suspended",
                Status = StatusCodes.Status403Forbidden,
                Instance = HttpContext.Request.Path
            });
        }

        if (User.IsInRole("MEMBER"))
        {
            var memberUserId = GetCurrentUserId();
            if (request.MemberUserId != memberUserId)
                return Forbid();
        }

        var url = await _stripe.CreateCheckoutSessionAsync(request, tenantId);
        return Ok(new { url });
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId))
            throw new InvalidOperationException("User id claim missing");
        return userId;
    }
}
