using System.ComponentModel.DataAnnotations;

namespace GymForYou.Api.DTOs;

public record CreateTenantRequest(
    [Required] string Name,
    [Required] string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    [Required] string OwnerName,
    [Required, EmailAddress] string OwnerEmail,
    [Required] string OwnerPassword,
    string? City = null,
    string? Address = null,
    string? Phone = null
);

public record UpdateTenantLocaleRequest([Required, RegularExpression("^(it|es)$")] string DefaultLocale);

public record ResolveTenantResponse(Guid TenantId, string Name, string DefaultLocale);

public record UpdateTenantBillingRequest(
    [Required, RegularExpression("^(PAID|UNPAID)$")] string BillingStatus,
    DateTime? BillingValidUntilUtc
);

public record UpdateTenantSuspensionRequest(
    [Required] bool IsSuspended
);

public record UpdatePlatformStaffRoleRequest(
    [Required] GymForYou.Api.Models.UserRole Role
);

public record UpdatePlatformStaffDisabledRequest(
    [Required] bool IsActive
);

public record UpdateTenantProfileRequest(
    [Required, MaxLength(120)] string Name,
    [MaxLength(120)] string? City,
    [MaxLength(200)] string? Address,
    [MaxLength(30)] string? Phone,
    [MaxLength(2000)] string? LogoUrl,
    [RegularExpression("^#([0-9a-fA-F]{6})$")] string? PrimaryColor,
    [RegularExpression("^#([0-9a-fA-F]{6})$")] string? SecondaryColor
);
