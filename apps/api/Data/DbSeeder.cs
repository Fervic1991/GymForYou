using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Tenants.AnyAsync()) return;

        var tenant = new Tenant
        {
            Name = "Gym Demo",
            Slug = "demo-gym",
            JoinCode = "DEMO123",
            City = "Milano",
            Address = "Via Demo 10",
            Phone = "+39 020000000",
            LogoUrl = "https://placehold.co/200x80?text=Gym+Demo",
            PrimaryColor = "#0ea5e9",
            SecondaryColor = "#111827",
            DefaultLocale = "it",
            BillingStatus = "PAID",
            BillingValidUntilUtc = DateTime.UtcNow.AddMonths(1),
            BillingLastUpdatedAtUtc = DateTime.UtcNow
        };

        var owner = new User
        {
            TenantId = tenant.Id,
            FullName = "Demo Owner",
            Email = "owner@gym.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner123!"),
            Role = UserRole.OWNER
        };

        var trainer = new User
        {
            TenantId = tenant.Id,
            FullName = "Demo Trainer",
            Email = "trainer@gym.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Trainer123!"),
            Role = UserRole.TRAINER
        };

        var members = Enumerable.Range(1, 5).Select(i => new User
        {
            TenantId = tenant.Id,
            FullName = $"Member {i}",
            Email = $"member{i}@gym.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Member123!"),
            Role = UserRole.MEMBER,
            Phone = $"+3900000000{i}"
        }).ToList();

        var monthly = new MembershipPlan { TenantId = tenant.Id, Name = "Mensile", Description = "Piano mensile", Price = 49m, Interval = "monthly" };
        var annual = new MembershipPlan { TenantId = tenant.Id, Name = "Annuale", Description = "Piano annuale", Price = 499m, Interval = "yearly" };

        db.Tenants.Add(tenant);
        db.TenantJoinLinks.Add(new TenantJoinLink
        {
            TenantId = tenant.Id,
            Code = "DEMO123",
            IsActive = true,
            UsesCount = 0
        });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant.Id, CancelCutoffHours = 6, MaxNoShows30d = 3, WeeklyBookingLimit = 8, BookingBlockDays = 7 });
        db.Users.AddRange(owner, trainer);
        db.Users.AddRange(members);
        await db.SaveChangesAsync();

        db.MemberProfiles.AddRange(members.Select(m => new MemberProfile
        {
            TenantId = tenant.Id,
            UserId = m.Id,
            Status = MemberStatus.ACTIVE,
            CheckInCode = Convert.ToHexString(Guid.NewGuid().ToByteArray())
        }));
        db.MembershipPlans.AddRange(monthly, annual);

        var c1 = new GymClass { TenantId = tenant.Id, Title = "Yoga", Description = "Classe yoga", TrainerUserId = trainer.Id, Capacity = 15, RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,WE" };
        var c2 = new GymClass { TenantId = tenant.Id, Title = "HIIT", Description = "Allenamento HIIT", TrainerUserId = trainer.Id, Capacity = 20, RecurrenceRule = "FREQ=WEEKLY;BYDAY=TU,TH" };
        var c3 = new GymClass { TenantId = tenant.Id, Title = "Pilates", Description = "Pilates base", TrainerUserId = trainer.Id, Capacity = 12, RecurrenceRule = "FREQ=WEEKLY;BYDAY=FR" };
        db.GymClasses.AddRange(c1, c2, c3);
        await db.SaveChangesAsync();

        db.MemberSubscriptions.AddRange(members.Select(m => new MemberSubscription
        {
            TenantId = tenant.Id,
            MemberUserId = m.Id,
            PlanId = monthly.Id,
            Status = SubscriptionStatus.ACTIVE,
            StartedAtUtc = DateTime.UtcNow.AddDays(-7),
            EndsAtUtc = DateTime.UtcNow.AddDays(23),
            IsManual = true
        }));

        var monday = DateTime.UtcNow.Date.AddDays(1);
        db.ClassSessions.AddRange(
            new ClassSession { TenantId = tenant.Id, GymClassId = c1.Id, StartAtUtc = monday.AddHours(17), EndAtUtc = monday.AddHours(18) },
            new ClassSession { TenantId = tenant.Id, GymClassId = c2.Id, StartAtUtc = monday.AddDays(1).AddHours(18), EndAtUtc = monday.AddDays(1).AddHours(19) },
            new ClassSession { TenantId = tenant.Id, GymClassId = c3.Id, StartAtUtc = monday.AddDays(2).AddHours(19), EndAtUtc = monday.AddDays(2).AddHours(20) }
        );

        db.ExerciseVideos.AddRange(
            new ExerciseVideo
            {
                TenantId = tenant.Id,
                Title = "Warm-up Full Body 10'",
                Category = "Warm-up",
                VideoUrl = "https://www.youtube.com/watch?v=UBMk30rjy0o",
                ThumbnailUrl = "https://i.ytimg.com/vi/UBMk30rjy0o/hqdefault.jpg",
                Description = "Riscaldamento completo per iniziare al meglio.",
                Provider = VideoProvider.YOUTUBE,
                DurationSeconds = 600,
                IsPublished = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new ExerciseVideo
            {
                TenantId = tenant.Id,
                Title = "Mobility Session 15'",
                Category = "Mobility",
                VideoUrl = "https://vimeo.com/76979871",
                Description = "Sessione mobilita per recupero e prevenzione infortuni.",
                Provider = VideoProvider.VIMEO,
                DurationSeconds = 900,
                IsPublished = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        );

        await db.SaveChangesAsync();
    }
}
