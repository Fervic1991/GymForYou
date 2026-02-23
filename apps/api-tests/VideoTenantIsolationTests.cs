using FluentAssertions;
using GymForYou.Api.Data;
using GymForYou.Api.Infrastructure;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Tests;

public class VideoTenantIsolationTests
{
    [Fact]
    public async Task SaveChanges_should_throw_when_video_has_different_tenant()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantProvider = new TenantProvider { TenantId = Guid.NewGuid() };
        var db = new AppDbContext(options, tenantProvider);

        db.ExerciseVideos.Add(new ExerciseVideo
        {
            TenantId = Guid.NewGuid(),
            Title = "Wrong tenant",
            Category = "Mobility",
            VideoUrl = "https://www.youtube.com/watch?v=UBMk30rjy0o",
            Provider = VideoProvider.YOUTUBE,
            DurationSeconds = 120
        });

        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task QueryFilter_should_return_only_current_tenant_videos()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedDb = new AppDbContext(options, new TenantProvider { TenantId = null }))
        {
            seedDb.ExerciseVideos.Add(new ExerciseVideo
            {
                TenantId = tenantA,
                Title = "Tenant A video",
                Category = "Strength",
                VideoUrl = "https://www.youtube.com/watch?v=UBMk30rjy0o",
                Provider = VideoProvider.YOUTUBE,
                DurationSeconds = 300
            });
            seedDb.ExerciseVideos.Add(new ExerciseVideo
            {
                TenantId = tenantB,
                Title = "Tenant B video",
                Category = "Cardio",
                VideoUrl = "https://vimeo.com/76979871",
                Provider = VideoProvider.VIMEO,
                DurationSeconds = 300
            });
            await seedDb.SaveChangesAsync();
        }

        await using var dbTenantA = new AppDbContext(options, new TenantProvider { TenantId = tenantA });
        var visible = await dbTenantA.ExerciseVideos.ToListAsync();

        visible.Should().HaveCount(1);
        visible[0].Title.Should().Be("Tenant A video");
    }
}
