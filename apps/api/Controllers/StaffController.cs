using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("staff")]
[Authorize(Roles = "OWNER,MANAGER")]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public StaffController(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.Users.Where(x => x.Role != UserRole.MEMBER).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffRequest request)
    {
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite(InviteStaffRequest request)
    {
        var tenantId = Guid.Parse(User.Claims.First(x => x.Type == "tenant_id").Value);
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var invite = new StaffInvite
        {
            Email = request.Email,
            Role = request.Role,
            InviteToken = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        _db.StaffInvites.Add(invite);
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(tenantId, request.Email, "staff_invite", new { token });
        return Ok(invite);
    }

    [AllowAnonymous]
    [HttpPost("invite/accept")]
    public async Task<IActionResult> AcceptInvite(AcceptInviteRequest request)
    {
        var invite = await _db.StaffInvites.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.InviteToken == request.Token && !x.Accepted);
        if (invite is null || invite.ExpiresAtUtc < DateTime.UtcNow) return BadRequest("Invalid invite");

        var user = new User
        {
            TenantId = invite.TenantId,
            Email = invite.Email,
            FullName = request.FullName,
            Phone = request.Phone,
            Role = invite.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        invite.Accepted = true;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }
}
