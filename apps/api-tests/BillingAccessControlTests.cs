using System.Security.Claims;
using FluentAssertions;
using GymForYou.Api.Controllers;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace GymForYou.Api.Tests;

public class BillingAccessControlTests
{
    [Fact]
    public async Task Member_subscriptions_returns_only_own_rows()
    {
        var tenant = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var controller = BuildController(db, tenant, memberA, "MEMBER");
        var result = await controller.Subscriptions();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<MemberSubscription>>().Subject.ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(x => x.MemberUserId == memberA);
    }

    [Fact]
    public async Task Member_payments_returns_only_own_rows()
    {
        var tenant = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var controller = BuildController(db, tenant, memberA, "MEMBER");
        var result = await controller.Payments();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<Payment>>().Subject.ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(x => x.MemberUserId == memberA);
    }

    [Fact]
    public async Task Member_checkout_for_other_member_is_forbidden()
    {
        var tenant = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var planId = await db.MembershipPlans.Select(x => x.Id).FirstAsync();
        var controller = BuildController(db, tenant, memberA, "MEMBER");

        var result = await controller.Checkout(new StartCheckoutRequest(
            MemberUserId: memberB,
            PlanId: planId,
            SuccessUrl: "http://localhost/success",
            CancelUrl: "http://localhost/cancel"));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Staff_subscriptions_returns_all_rows_for_tenant()
    {
        var tenant = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var controller = BuildController(db, tenant, staffId, "OWNER");
        var result = await controller.Subscriptions();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<MemberSubscription>>().Subject.ToList();

        rows.Should().HaveCount(2);
        rows.Select(x => x.MemberUserId).Should().Contain(memberA);
        rows.Select(x => x.MemberUserId).Should().Contain(memberB);
    }

    [Fact]
    public async Task Staff_payments_returns_all_rows_for_tenant()
    {
        var tenant = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var controller = BuildController(db, tenant, staffId, "OWNER");
        var result = await controller.Payments();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<Payment>>().Subject.ToList();

        rows.Should().HaveCount(2);
        rows.Select(x => x.MemberUserId).Should().Contain(memberA);
        rows.Select(x => x.MemberUserId).Should().Contain(memberB);
    }

    [Fact]
    public async Task Member_cannot_export_payments_csv()
    {
        var tenant = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB);

        var controller = BuildController(db, tenant, memberA, "MEMBER");
        var result = await controller.ExportPaymentsCsv();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Checkout_is_blocked_when_tenant_is_suspended()
    {
        var tenant = Guid.NewGuid();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var db = await BuildBillingScenarioAsync(tenant, memberA, memberB, tenantSuspended: true);

        var planId = await db.MembershipPlans.Select(x => x.Id).FirstAsync();
        var controller = BuildController(db, tenant, memberA, "MEMBER");

        var result = await controller.Checkout(new StartCheckoutRequest(
            MemberUserId: memberA,
            PlanId: planId,
            SuccessUrl: "http://localhost/success",
            CancelUrl: "http://localhost/cancel"));

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
        obj.Value.Should().NotBeNull();
    }

    private static async Task<AppDbContext> BuildBillingScenarioAsync(Guid tenant, Guid memberAId, Guid memberBId, bool tenantSuspended = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TenantProvider { TenantId = tenant });

        db.Tenants.Add(new Tenant
        {
            Id = tenant,
            Name = "Gym Billing",
            Slug = "gym-billing",
            DefaultLocale = "it",
            IsSuspended = tenantSuspended,
            BillingStatus = "PAID",
            BillingValidUntilUtc = DateTime.UtcNow.AddDays(30)
        });

        var memberA = new User { TenantId = tenant, Id = memberAId, Email = "a@g.com", FullName = "A", PasswordHash = "x", Role = UserRole.MEMBER };
        var memberB = new User { TenantId = tenant, Id = memberBId, Email = "b@g.com", FullName = "B", PasswordHash = "x", Role = UserRole.MEMBER };
        db.Users.AddRange(memberA, memberB);

        var plan = new MembershipPlan { TenantId = tenant, Name = "Mensile", Description = "", Price = 49m, Interval = "monthly", IsActive = true };
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        var subA = new MemberSubscription { TenantId = tenant, MemberUserId = memberAId, PlanId = plan.Id, Status = SubscriptionStatus.ACTIVE };
        var subB = new MemberSubscription { TenantId = tenant, MemberUserId = memberBId, PlanId = plan.Id, Status = SubscriptionStatus.PAST_DUE };
        db.MemberSubscriptions.AddRange(subA, subB);
        await db.SaveChangesAsync();

        db.Payments.AddRange(
            new Payment { TenantId = tenant, MemberUserId = memberAId, SubscriptionId = subA.Id, Amount = 49m, Currency = "eur", Status = "paid", Method = GymForYou.Api.Models.PaymentMethod.STRIPE },
            new Payment { TenantId = tenant, MemberUserId = memberBId, SubscriptionId = subB.Id, Amount = 49m, Currency = "eur", Status = "failed", Method = GymForYou.Api.Models.PaymentMethod.STRIPE }
        );
        await db.SaveChangesAsync();

        return db;
    }

    private static BillingController BuildController(AppDbContext db, Guid tenantId, Guid userId, string role)
    {
        var controller = new BillingController(db, new NoopStripeService());
        controller.ControllerContext = BuildContext(tenantId, userId, role);
        return controller;
    }

    private static ControllerContext BuildContext(Guid tenantId, Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "test");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private class NoopStripeService : IStripeService
    {
        public Task<string> CreateCheckoutSessionAsync(StartCheckoutRequest request, Guid tenantId) => Task.FromResult("https://stripe.test/checkout");
        public Event VerifyAndConstructEvent(string payload, string signatureHeader) => throw new NotImplementedException();
        public Task HandleWebhookAsync(Event stripeEvent) => Task.CompletedTask;
    }
}
