using GymForYou.Api.DTOs;
using GymForYou.Api.Data;
using GymForYou.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AppDbContext? _db;

    public AuthController(IAuthService auth, AppDbContext? db = null)
    {
        _auth = auth;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!request.TenantId.HasValue && string.IsNullOrWhiteSpace(request.TenantSlug))
            return BadRequest("tenantSlug or tenantId is required");

        if (await IsLoginTenantSuspendedAsync(request))
            return TenantSuspendedProblem();

        var result = await _auth.LoginAsync(request);
        if (result.Auth is not null) return Ok(result.Auth);
        if (result.StatusCode.HasValue)
        {
            return StatusCode(result.StatusCode.Value, new ProblemDetails
            {
                Title = result.Title ?? "Access denied",
                Detail = result.Detail,
                Status = result.StatusCode.Value,
                Instance = HttpContext.Request.Path
            });
        }
        return Unauthorized();
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request.RefreshToken);
        if (result is null) return Unauthorized();
        return Ok(result);
    }

    [HttpPost("register-member")]
    [EnableRateLimiting("onboarding")]
    public async Task<ActionResult<AuthResponse>> RegisterMember(RegisterMemberRequest request)
    {
        if (await IsRegisterTenantSuspendedAsync(request.JoinCode))
            return TenantSuspendedProblem();

        var result = await _auth.RegisterMemberAsync(request);
        if (result.Auth is not null) return Ok(result.Auth);
        return StatusCode(result.StatusCode, new { error = result.Error });
    }

    [HttpPost("platform/login")]
    public async Task<ActionResult<PlatformAuthResponse>> PlatformLogin(PlatformLoginRequest request)
    {
        var result = await _auth.PlatformLoginAsync(request);
        if (result is null) return Unauthorized();
        return Ok(result);
    }

    private async Task<bool> IsLoginTenantSuspendedAsync(LoginRequest request)
    {
        if (_db is null) return false;
        Guid? tenantId = request.TenantId;
        if (!tenantId.HasValue && !string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            tenantId = await _db.Tenants.IgnoreQueryFilters()
                .Where(x => x.Slug == request.TenantSlug!.Trim().ToLowerInvariant())
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
        }
        if (!tenantId.HasValue) return false;
        return await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId.Value && x.IsSuspended);
    }

    private async Task<bool> IsRegisterTenantSuspendedAsync(string joinCode)
    {
        if (_db is null) return false;
        var normalized = joinCode.Trim().ToUpperInvariant();
        var tenantId = await _db.TenantJoinLinks.IgnoreQueryFilters()
            .Where(x => x.Code == normalized)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync();
        if (!tenantId.HasValue) return false;
        return await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId.Value && x.IsSuspended);
    }

    private ObjectResult TenantSuspendedProblem()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Tenant suspended",
            Detail = "Tenant suspended",
            Status = StatusCodes.Status403Forbidden,
            Instance = HttpContext.Request.Path
        });
    }
}
