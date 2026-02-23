using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid CurrentTenantId => _tenantProvider.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<StaffInvite> StaffInvites => Set<StaffInvite>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<GymClass> GymClasses => Set<GymClass>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<SessionException> SessionExceptions => Set<SessionException>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<ExerciseVideo> ExerciseVideos => Set<ExerciseVideo>();
    public DbSet<VideoProgress> VideoProgresses => Set<VideoProgress>();
    public DbSet<TenantJoinLink> TenantJoinLinks => Set<TenantJoinLink>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Tenant>().HasIndex(x => x.JoinCode).IsUnique();
        modelBuilder.Entity<TenantJoinLink>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<TenantJoinLink>().HasIndex(x => new { x.TenantId, x.IsActive, x.CreatedAtUtc });
        modelBuilder.Entity<User>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.Token).IsUnique();
        modelBuilder.Entity<StaffInvite>().HasIndex(x => x.InviteToken).IsUnique();
        modelBuilder.Entity<MemberProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<MemberProfile>().HasIndex(x => new { x.TenantId, x.CheckInCode }).IsUnique();
        modelBuilder.Entity<Booking>().HasIndex(x => new { x.TenantId, x.SessionId, x.MemberUserId }).IsUnique();
        modelBuilder.Entity<Booking>().HasIndex(x => new { x.TenantId, x.SessionId, x.Status });
        modelBuilder.Entity<Booking>().HasIndex(x => new { x.TenantId, x.MemberUserId, x.Status, x.CreatedAtUtc });
        modelBuilder.Entity<ClassSession>().HasIndex(x => new { x.TenantId, x.StartAtUtc });
        modelBuilder.Entity<MemberSubscription>().HasIndex(x => new { x.TenantId, x.StripeSubscriptionId });
        modelBuilder.Entity<ExerciseVideo>().HasIndex(x => new { x.TenantId, x.Category });
        modelBuilder.Entity<ExerciseVideo>().HasIndex(x => new { x.TenantId, x.IsPublished, x.CreatedAtUtc });
        modelBuilder.Entity<VideoProgress>().HasIndex(x => new { x.TenantId, x.VideoId, x.MemberUserId }).IsUnique();
        modelBuilder.Entity<VideoProgress>().HasIndex(x => new { x.TenantId, x.MemberUserId, x.LastViewedAtUtc });
        modelBuilder.Entity<Payment>().HasIndex(x => x.StripeInvoiceId).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(x => x.StripePaymentIntentId).IsUnique();
        modelBuilder.Entity<WebhookEventLog>().HasIndex(x => new { x.Provider, x.StripeEventId }).IsUnique();
        modelBuilder.Entity<TenantSettings>().HasIndex(x => x.TenantId).IsUnique();

        modelBuilder.Entity<User>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<TenantSettings>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<StaffInvite>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<MemberProfile>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<MembershipPlan>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<MemberSubscription>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<GymClass>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<ClassSession>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<SessionException>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Booking>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<CheckIn>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<ExerciseVideo>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<VideoProgress>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<TenantJoinLink>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<NotificationLog>().HasQueryFilter(x => x.TenantId == CurrentTenantId);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.TenantId;
        foreach (var e in ChangeTracker.Entries<ITenantEntity>())
        {
            if (e.State == EntityState.Added)
            {
                if (tenantId.HasValue && e.Entity.TenantId == Guid.Empty)
                    e.Entity.TenantId = tenantId.Value;
            }

            if (tenantId.HasValue && e.Entity.TenantId != tenantId.Value)
                throw new InvalidOperationException("Tenant isolation violation");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
