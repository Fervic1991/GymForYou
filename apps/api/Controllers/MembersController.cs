using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("members")]
[Authorize(Roles = "OWNER,MANAGER,TRAINER")]
public class MembersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICheckInService _checkInService;

    public MembersController(AppDbContext db, ICheckInService checkInService)
    {
        _db = db;
        _checkInService = checkInService;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.MemberProfiles.Join(_db.Users, m => m.UserId, u => u.Id, (m, u) => new
    {
        u.Id,
        u.FullName,
        u.Email,
        u.Phone,
        m.Status,
        m.LastCheckInUtc,
        m.BookingBlockedUntilUtc
    }).ToListAsync());

    [HttpGet("{memberUserId:guid}/checkins")]
    public async Task<IActionResult> CheckInHistory(Guid memberUserId)
    {
        var history = await _db.CheckIns.Where(x => x.MemberUserId == memberUserId).OrderByDescending(x => x.CheckInAtUtc).Take(200).ToListAsync();
        return Ok(history);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMemberRequest request)
    {
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.MEMBER
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = new MemberProfile { UserId = user.Id, Status = MemberStatus.ACTIVE, CheckInCode = Convert.ToHexString(Guid.NewGuid().ToByteArray()) };
        _db.MemberProfiles.Add(profile);
        await _db.SaveChangesAsync();

        return Ok(new { user.Id });
    }

    [HttpPatch("{memberUserId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid memberUserId, UpdateMemberStatusRequest request)
    {
        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.UserId == memberUserId);
        if (profile is null) return NotFound();
        profile.Status = request.Status;
        await _db.SaveChangesAsync();
        return Ok(profile);
    }

    [HttpPost("{memberUserId:guid}/checkin-qr")]
    public async Task<IActionResult> GenerateQr(Guid memberUserId, [FromBody] GenerateQrRequest request)
    {
        var result = await _checkInService.GenerateQrCodeAsync(request with { MemberUserId = memberUserId });
        return Ok(new { code = result.code, expiresAtUtc = result.expiresAtUtc });
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn(CheckInRequest request)
    {
        var checkIn = await _checkInService.ManualCheckInAsync(request.MemberUserId, "manual");
        return Ok(checkIn);
    }

    [HttpPost("checkin/qr")]
    public async Task<IActionResult> CheckInWithQr(QrCheckInRequest request)
    {
        var checkIn = await _checkInService.CheckInByCodeAsync(request.Code, request.Source);
        return Ok(checkIn);
    }
}
