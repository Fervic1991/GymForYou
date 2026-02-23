using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace GymForYou.Api.Tests;

public class StripeWebhookTests
{
    [Fact]
    public void Should_reject_invalid_signature()
    {
        var db = CreateDb(Guid.NewGuid());
        var sut = CreateSut(db);
        var payload = "{\"id\":\"evt_x\",\"object\":\"event\"}";

        FluentActions.Invoking(() => sut.VerifyAndConstructEvent(payload, "bad_signature"))
            .Should().Throw<Exception>();
    }

    [Fact]
    public async Task Duplicate_checkout_event_should_not_duplicate_subscription()
    {
        var tenant = Guid.NewGuid();
        var db = CreateDb(tenant);
        var sut = CreateSut(db);

        var member = new User { TenantId = tenant, Email = "m@g.com", FullName = "M", PasswordHash = "x", Role = UserRole.MEMBER };
        var plan = new MembershipPlan { TenantId = tenant, Name = "Mensile", Price = 49, Interval = "monthly" };
        db.Users.Add(member);
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        var evt = new Event
        {
            Id = "evt_checkout_dup",
            Type = "checkout.session.completed",
            Data = new EventData
            {
                Object = new Stripe.Checkout.Session
                {
                    Id = "cs_test_1",
                    CustomerId = "cus_1",
                    SubscriptionId = "sub_1",
                    Metadata = new Dictionary<string, string>
                    {
                        ["TenantId"] = tenant.ToString(),
                        ["MemberId"] = member.Id.ToString(),
                        ["PlanId"] = plan.Id.ToString()
                    }
                }
            }
        };

        await sut.HandleWebhookAsync(evt);
        await sut.HandleWebhookAsync(evt);

        (await db.WebhookEventLogs.CountAsync()).Should().Be(1);
        var subscriptions = await db.MemberSubscriptions.IgnoreQueryFilters().ToListAsync();
        subscriptions.Should().HaveCount(1);
        subscriptions[0].StripeSubscriptionId.Should().Be("sub_1");
        subscriptions[0].Status.Should().Be(SubscriptionStatus.ACTIVE);
    }

    [Fact]
    public async Task Checkout_completed_tenant_mismatch_should_skip_without_side_effects()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = null });
        var sut = CreateSut(db);

        var memberFromOtherTenant = new User { TenantId = tenantB, Email = "m2@g.com", FullName = "M2", PasswordHash = "x", Role = UserRole.MEMBER };
        var planTenantA = new MembershipPlan { TenantId = tenantA, Name = "Mensile", Price = 49, Interval = "monthly" };
        db.Users.Add(memberFromOtherTenant);
        db.MembershipPlans.Add(planTenantA);
        await db.SaveChangesAsync();

        var evt = new Event
        {
            Id = "evt_checkout_mismatch",
            Type = "checkout.session.completed",
            Data = new EventData
            {
                Object = new Stripe.Checkout.Session
                {
                    Id = "cs_test_mismatch",
                    CustomerId = "cus_mm",
                    SubscriptionId = "sub_mm",
                    Metadata = new Dictionary<string, string>
                    {
                        ["TenantId"] = tenantA.ToString(),
                        ["MemberId"] = memberFromOtherTenant.Id.ToString(),
                        ["PlanId"] = planTenantA.Id.ToString()
                    }
                }
            }
        };

        await sut.HandleWebhookAsync(evt);

        var subCount = await db.MemberSubscriptions.IgnoreQueryFilters().CountAsync();
        var payCount = await db.Payments.IgnoreQueryFilters().CountAsync();
        subCount.Should().Be(0);
        payCount.Should().Be(0);

        var log = await db.WebhookEventLogs.FirstAsync(x => x.StripeEventId == "evt_checkout_mismatch");
        log.Outcome.Should().Be("skipped_mismatch");
    }

    [Fact]
    public async Task Duplicate_invoice_paid_event_should_not_duplicate_payment()
    {
        var tenant = Guid.NewGuid();
        var db = CreateDb(tenant);
        var sut = CreateSut(db);

        var member = new User { TenantId = tenant, Email = "m@g.com", FullName = "M", PasswordHash = "x", Role = UserRole.MEMBER };
        var plan = new MembershipPlan { TenantId = tenant, Name = "Mensile", Price = 49, Interval = "monthly" };
        db.Users.Add(member);
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        var sub = new MemberSubscription
        {
            TenantId = tenant,
            MemberUserId = member.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.INCOMPLETE,
            StripeSubscriptionId = "sub_2"
        };
        db.MemberSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var evt = new Event
        {
            Id = "evt_invoice_dup",
            Type = "invoice.payment_succeeded",
            Data = new EventData
            {
                Object = new Invoice
                {
                    Id = "in_1",
                    SubscriptionId = "sub_2",
                    AmountPaid = 4900,
                    AmountDue = 4900,
                    Currency = "eur",
                    PaymentIntentId = "pi_1"
                }
            }
        };

        await sut.HandleWebhookAsync(evt);
        await sut.HandleWebhookAsync(evt);

        var updated = await db.MemberSubscriptions.IgnoreQueryFilters().FirstAsync(x => x.Id == sub.Id);
        updated.Status.Should().Be(SubscriptionStatus.ACTIVE);

        var payments = await db.Payments.IgnoreQueryFilters().Where(x => x.StripeInvoiceId == "in_1").ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].Status.Should().Be("paid");
    }

    private static AppDbContext CreateDb(Guid tenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options, new TenantProvider { TenantId = tenant });
    }

    private static StripeService CreateSut(AppDbContext db)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["STRIPE_SECRET_KEY"] = "sk_test_x",
            ["STRIPE_WEBHOOK_SECRET"] = "whsec_valid"
        }).Build();

        return new StripeService(config, db, NullLogger<StripeService>.Instance);
    }
}
