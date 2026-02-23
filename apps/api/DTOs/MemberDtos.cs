using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreateMemberRequest(
    [Required, MinLength(5)] string FullName,
    [Required, EmailAddress, RegularExpression("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")] string Email,
    [RegularExpression("^\\d+$")] string? Phone,
    [Required] string Password
);
public record UpdateMemberStatusRequest([Required] MemberStatus Status);
public record CheckInRequest([Required] Guid MemberUserId);
public record GenerateQrRequest([Required] Guid MemberUserId, bool Rotate = false, int ExpiresInMinutes = 60);
public record QrCheckInRequest([Required] string Code, string Source = "qr");
