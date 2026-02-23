using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreateVideoRequest(
    [Required, MaxLength(140)] string Title,
    [Required, MaxLength(80)] string Category,
    [Required, Url, MaxLength(2000)] string VideoUrl,
    [Url, MaxLength(2000)] string? ThumbnailUrl,
    string Description,
    [Required] VideoProvider Provider,
    [Range(0, 60 * 60 * 12)] int DurationSeconds,
    bool IsPublished = true
);

public record UpdateVideoRequest(
    [Required, MaxLength(140)] string Title,
    [Required, MaxLength(80)] string Category,
    [Required, Url, MaxLength(2000)] string VideoUrl,
    [Url, MaxLength(2000)] string? ThumbnailUrl,
    string Description,
    [Required] VideoProvider Provider,
    [Range(0, 60 * 60 * 12)] int DurationSeconds,
    bool IsPublished = true
);

public record TrackVideoProgressRequest(
    [Range(0, 60 * 60 * 12)] int WatchedSeconds,
    bool Completed
);
