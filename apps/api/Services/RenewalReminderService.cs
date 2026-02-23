using GymForYou.Api.Data;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Services;

public interface IRenewalReminderService
{
    Task<int> SendForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<int> SendForAllTenantsAsync(CancellationToken cancellationToken = default);
}

public class RenewalReminderService : IRenewalReminderService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<RenewalReminderService> _logger;

    public RenewalReminderService(AppDbContext db, INotificationService notifications, ILogger<RenewalReminderService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> SendForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var end = now.AddDays(7);
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

        var rows = await _db.MemberSubscriptions.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId
                        && s.Status == SubscriptionStatus.ACTIVE
                        && s.EndsAtUtc.HasValue
                        && s.EndsAtUtc.Value >= now
                        && s.EndsAtUtc.Value <= end)
            .Join(_db.Users.IgnoreQueryFilters(),
                s => new { s.TenantId, UserId = s.MemberUserId },
                u => new { u.TenantId, UserId = u.Id },
                (s, u) => new { Subscription = s, User = u })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return 0;

        var toSend = 0;
        foreach (var row in rows)
        {
            var marker = $"\"subscriptionId\":\"{row.Subscription.Id}\"";
            var alreadySentToday = await _db.NotificationLogs.IgnoreQueryFilters().AnyAsync(n =>
                n.TenantId == tenantId
                && n.Type == "renewal_reminder"
                && n.SentAtUtc >= todayStart
                && n.SentAtUtc <= now
                && n.ToEmail == row.User.Email
                && n.Payload.Contains(marker), cancellationToken);

            if (alreadySentToday) continue;

            await _notifications.SendAsync(
                tenantId,
                row.User.Email,
                "renewal_reminder",
                new
                {
                    subscriptionId = row.Subscription.Id,
                    memberUserId = row.Subscription.MemberUserId,
                    endsAtUtc = row.Subscription.EndsAtUtc,
                    daysLeft = Math.Max(0, (int)Math.Ceiling((row.Subscription.EndsAtUtc!.Value - now).TotalDays))
                });
            toSend += 1;
        }

        _logger.LogInformation("Renewal reminders sent. TenantId={TenantId} Sent={Sent}", tenantId, toSend);
        return toSend;
    }

    public async Task<int> SendForAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => !t.IsSuspended)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var total = 0;
        foreach (var tenantId in tenantIds)
        {
            total += await SendForTenantAsync(tenantId, cancellationToken);
        }

        return total;
    }
}

