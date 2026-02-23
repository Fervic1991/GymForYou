namespace GymForYou.Api.Infrastructure;

public interface ITenantProvider
{
    Guid? TenantId { get; set; }
}

public class TenantProvider : ITenantProvider
{
    public Guid? TenantId { get; set; }
}
