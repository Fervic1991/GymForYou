using GymForYou.Api.Data;
using GymForYou.Api.DTOs;
using GymForYou.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshAsync(string refreshToken);
    Task<RegisterMemberResult> RegisterMemberAsync(RegisterMemberRequest request);
    Task<PlatformAuthResponse?> PlatformLoginAsync(PlatformLoginRequest request);
}

public record RegisterMemberResult(AuthResponse? Auth, int StatusCode, string? Error);
public record LoginResult(AuthResponse? Auth, int? StatusCode = null, string? Title = null, string? Detail = null);

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IJwtTokenService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        Guid? tenantId = request.TenantId;
        var slug = request.TenantSlug?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var resolvedTenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug);
            if (resolvedTenant is null) return new LoginResult(null);
            tenantId = resolvedTenant.Id;
        }

        if (!tenantId.HasValue) return new LoginResult(null);

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId.Value);
        if (tenant is null || tenant.IsSuspended) return new LoginResult(null);
        if (!string.Equals(tenant.BillingStatus, "PAID", StringComparison.OrdinalIgnoreCase)) return new LoginResult(null);
        if (tenant.BillingValidUntilUtc.HasValue && tenant.BillingValidUntilUtc.Value < DateTime.UtcNow) return new LoginResult(null);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
            x.TenantId == tenantId.Value && x.Email == email && x.Role == request.Role);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new LoginResult(null);
        if (!user.IsActive)
            return new LoginResult(
                null,
                StatusCodes.Status403Forbidden,
                "Account non attivo",
                "Il tuo account e disattivato. Contatta la palestra.");

        var (access, exp) = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = refresh,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        return new LoginResult(new AuthResponse(access, refresh, exp, user.Role, user.Id, user.TenantId));
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var rt = await _db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Token == refreshToken && x.RevokedAtUtc == null);
        if (rt is null || rt.ExpiresAtUtc <= DateTime.UtcNow)
            return null;

        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == rt.UserId && x.IsActive);
        if (user is null)
            return null;

        rt.RevokedAtUtc = DateTime.UtcNow;

        var (access, exp) = _jwt.CreateAccessToken(user);
        var nextRefresh = _jwt.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = nextRefresh,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(access, nextRefresh, exp, user.Role, user.Id, user.TenantId);
    }

    public Task<PlatformAuthResponse?> PlatformLoginAsync(PlatformLoginRequest request)
    {
        var expectedEmail = (_config["PLATFORM_ADMIN_EMAIL"] ?? "superadmin@gym.local").Trim().ToLowerInvariant();
        var expectedPassword = _config["PLATFORM_ADMIN_PASSWORD"] ?? "SuperAdmin123!";
        var email = request.Email.Trim().ToLowerInvariant();

        if (!string.Equals(email, expectedEmail, StringComparison.Ordinal) || request.Password != expectedPassword)
            return Task.FromResult<PlatformAuthResponse?>(null);

        var (token, exp) = _jwt.CreatePlatformAccessToken(email);
        return Task.FromResult<PlatformAuthResponse?>(new PlatformAuthResponse(token, exp, UserRole.PLATFORM_ADMIN));
    }

    public async Task<RegisterMemberResult> RegisterMemberAsync(RegisterMemberRequest request)
    {
        var joinCode = request.JoinCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(joinCode))
            return new RegisterMemberResult(null, 400, "joinCode is required");

        var link = await _db.TenantJoinLinks.IgnoreQueryFilters()
            .Where(x => x.Code == joinCode)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (link is null)
            return new RegisterMemberResult(null, 404, "Join link not found");

        if (!link.IsActive)
            return new RegisterMemberResult(null, 403, "Join link inactive");

        if (link.ExpiresAtUtc.HasValue && DateTime.UtcNow > link.ExpiresAtUtc.Value)
            return new RegisterMemberResult(null, 403, "Join link expired");

        if (link.MaxUses.HasValue && link.UsesCount >= link.MaxUses.Value)
            return new RegisterMemberResult(null, 403, "Join link max uses reached");

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == link.TenantId);

        if (tenant is null)
            return new RegisterMemberResult(null, 404, "Tenant not found");

        if (tenant.IsSuspended)
            return new RegisterMemberResult(null, 403, "Tenant suspended");

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenant.Id && x.Email == email);
        if (exists)
            return new RegisterMemberResult(null, 409, "Email already registered for this tenant");

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.MEMBER
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.MemberProfiles.Add(new MemberProfile
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Status = MemberStatus.ACTIVE,
            CheckInCode = Convert.ToHexString(Guid.NewGuid().ToByteArray())
        });
        await _db.SaveChangesAsync();

        var (access, exp) = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Token = refresh,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        });

        link.UsesCount += 1;
        await _db.SaveChangesAsync();

        return new RegisterMemberResult(new AuthResponse(access, refresh, exp, user.Role, user.Id, user.TenantId), 200, null);
    }
}
