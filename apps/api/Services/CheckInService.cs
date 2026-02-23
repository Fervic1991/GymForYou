using System.Security.Cryptography;
using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Services;

public interface ICheckInService
{
    Task<(string code, DateTime? expiresAtUtc)> GenerateQrCodeAsync(GenerateQrRequest request);
    Task<CheckIn> CheckInByCodeAsync(string code, string source);
    Task<CheckIn> ManualCheckInAsync(Guid memberUserId, string source);
}

public class CheckInService : ICheckInService
{
    private readonly AppDbContext _db;

    public CheckInService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(string code, DateTime? expiresAtUtc)> GenerateQrCodeAsync(GenerateQrRequest request)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.UserId == request.MemberUserId)
            ?? throw new InvalidOperationException("Member not found");

        if (request.Rotate || string.IsNullOrWhiteSpace(member.CheckInCode))
            member.CheckInCode = CreateCode();

        member.CheckInCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(request.ExpiresInMinutes, 1, 1440));
        await _db.SaveChangesAsync();

        return (member.CheckInCode, member.CheckInCodeExpiresAtUtc);
    }

    public async Task<CheckIn> CheckInByCodeAsync(string code, string source)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.CheckInCode == code)
            ?? throw new InvalidOperationException("QR code invalid");

        if (member.CheckInCodeExpiresAtUtc.HasValue && member.CheckInCodeExpiresAtUtc.Value < DateTime.UtcNow)
            throw new InvalidOperationException("QR code expired");

        return await ManualCheckInAsync(member.UserId, source);
    }

    public async Task<CheckIn> ManualCheckInAsync(Guid memberUserId, string source)
    {
        var profile = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.UserId == memberUserId)
            ?? throw new InvalidOperationException("Member not found");

        if (profile.Status != MemberStatus.ACTIVE)
            throw new InvalidOperationException("Member suspended");

        var activeSubscription = await _db.MemberSubscriptions
            .AnyAsync(x => x.MemberUserId == memberUserId && x.Status == SubscriptionStatus.ACTIVE && (!x.EndsAtUtc.HasValue || x.EndsAtUtc >= DateTime.UtcNow));

        if (!activeSubscription)
            throw new InvalidOperationException("Subscription is not active");

        profile.LastCheckInUtc = DateTime.UtcNow;
        var checkIn = new CheckIn { MemberUserId = memberUserId, Source = source };
        _db.CheckIns.Add(checkIn);
        await _db.SaveChangesAsync();
        return checkIn;
    }

    private static string CreateCode()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    }
}
