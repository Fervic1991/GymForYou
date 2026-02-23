using GymForYou.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreateStaffRequest([Required] string FullName, [Required, EmailAddress] string Email, string? Phone, [Required] string Password, [Required] UserRole Role);
public record InviteStaffRequest([Required, EmailAddress] string Email, [Required] UserRole Role);
public record AcceptInviteRequest([Required] string Token, [Required] string FullName, [Required] string Password, string? Phone);
