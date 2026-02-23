using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record LoginRequest(
    Guid? TenantId,
    string? TenantSlug,
    [Required, EmailAddress, RegularExpression("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")] string Email,
    [Required] string Password,
    [Required] UserRole Role);

public record RefreshRequest([Required] string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserRole Role, Guid UserId, Guid TenantId);

public record RegisterMemberRequest(
    [Required] string JoinCode,
    [Required, MinLength(5)] string FullName,
    [Required, EmailAddress, RegularExpression("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")] string Email,
    [RegularExpression("^\\d+$")] string? Phone,
    [Required] string Password
);

public record PlatformLoginRequest(
    [Required, EmailAddress, RegularExpression("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")] string Email,
    [Required] string Password
);

public record PlatformAuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserRole Role
);
