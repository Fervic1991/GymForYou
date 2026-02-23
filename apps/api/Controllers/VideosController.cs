using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymForYou.Api.Controllers;

[ApiController]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _db;

    public VideosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("videos")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> StaffList()
        => Ok(await _db.ExerciseVideos.OrderByDescending(x => x.CreatedAtUtc).ToListAsync());

    [HttpPost("videos")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Create(CreateVideoRequest request)
    {
        var video = new ExerciseVideo
        {
            Title = request.Title,
            Category = request.Category,
            VideoUrl = request.VideoUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Description = request.Description,
            Provider = request.Provider,
            DurationSeconds = request.DurationSeconds,
            IsPublished = request.IsPublished,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.ExerciseVideos.Add(video);
        await _db.SaveChangesAsync();
        return Ok(video);
    }

    [HttpPut("videos/{id:guid}")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Update(Guid id, UpdateVideoRequest request)
    {
        var video = await _db.ExerciseVideos.FirstOrDefaultAsync(x => x.Id == id);
        if (video is null) return NotFound();

        video.Title = request.Title;
        video.Category = request.Category;
        video.VideoUrl = request.VideoUrl;
        video.ThumbnailUrl = request.ThumbnailUrl;
        video.Description = request.Description;
        video.Provider = request.Provider;
        video.DurationSeconds = request.DurationSeconds;
        video.IsPublished = request.IsPublished;
        video.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(video);
    }

    [HttpDelete("videos/{id:guid}")]
    [Authorize(Roles = "OWNER,MANAGER,TRAINER")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var video = await _db.ExerciseVideos.FirstOrDefaultAsync(x => x.Id == id);
        if (video is null) return NotFound();

        var progresses = await _db.VideoProgresses.Where(x => x.VideoId == id).ToListAsync();
        if (progresses.Count > 0) _db.VideoProgresses.RemoveRange(progresses);
        _db.ExerciseVideos.Remove(video);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("app/videos")]
    [Authorize(Roles = "MEMBER")]
    public async Task<IActionResult> MemberList([FromQuery] string? category = null)
    {
        var userId = GetCurrentUserId();
        var q = _db.ExerciseVideos.Where(x => x.IsPublished);
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(x => x.Category == category);

        var videos = await q
            .OrderBy(x => x.Category)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(v => new
            {
                v.Id,
                v.Title,
                v.Category,
                v.VideoUrl,
                v.ThumbnailUrl,
                v.Description,
                v.Provider,
                v.DurationSeconds
            })
            .ToListAsync();

        var progress = await _db.VideoProgresses
            .Where(x => x.MemberUserId == userId)
            .ToDictionaryAsync(x => x.VideoId, x => x);

        var result = videos.Select(v =>
        {
            progress.TryGetValue(v.Id, out var p);
            var watched = p?.WatchedSeconds ?? 0;
            var pct = v.DurationSeconds > 0 ? Math.Min(100, (int)Math.Round(watched * 100d / v.DurationSeconds)) : 0;
            return new
            {
                v.Id,
                v.Title,
                v.Category,
                v.VideoUrl,
                v.ThumbnailUrl,
                v.Description,
                v.Provider,
                v.DurationSeconds,
                WatchedSeconds = watched,
                ProgressPercent = pct,
                Completed = p?.Completed ?? false
            };
        });

        return Ok(result);
    }

    [HttpPost("videos/{id:guid}/progress")]
    [Authorize(Roles = "MEMBER")]
    public async Task<IActionResult> TrackProgress(Guid id, TrackVideoProgressRequest request)
    {
        var userId = GetCurrentUserId();
        var video = await _db.ExerciseVideos.FirstOrDefaultAsync(x => x.Id == id && x.IsPublished);
        if (video is null) return NotFound("Video not found");

        var watchedSeconds = Math.Max(0, Math.Min(request.WatchedSeconds, video.DurationSeconds > 0 ? video.DurationSeconds : request.WatchedSeconds));
        var progress = await _db.VideoProgresses.FirstOrDefaultAsync(x => x.VideoId == id && x.MemberUserId == userId);
        if (progress is null)
        {
            progress = new VideoProgress
            {
                VideoId = id,
                MemberUserId = userId,
                WatchedSeconds = watchedSeconds,
                Completed = request.Completed || (video.DurationSeconds > 0 && watchedSeconds >= video.DurationSeconds),
                LastViewedAtUtc = DateTime.UtcNow
            };
            _db.VideoProgresses.Add(progress);
        }
        else
        {
            progress.WatchedSeconds = Math.Max(progress.WatchedSeconds, watchedSeconds);
            progress.Completed = progress.Completed || request.Completed || (video.DurationSeconds > 0 && progress.WatchedSeconds >= video.DurationSeconds);
            progress.LastViewedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(progress);
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId))
            throw new InvalidOperationException("User id claim missing");
        return userId;
    }
}
