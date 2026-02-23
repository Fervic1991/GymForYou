using GymForYou.Api.Data;
using GymForYou.Api.Models;

namespace GymForYou.Api.Services;

public interface INotificationService
{
    Task SendAsync(Guid tenantId, string toEmail, string type, object payload);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly AppDbContext _db;

    public NotificationService(ILogger<NotificationService> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task SendAsync(Guid tenantId, string toEmail, string type, object payload)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(payload);
        _logger.LogInformation("EMAIL {Type} to {To}: {Body}", type, toEmail, body);

        _db.NotificationLogs.Add(new NotificationLog
        {
            TenantId = tenantId,
            ToEmail = toEmail,
            Type = type,
            Payload = body
        });
        await _db.SaveChangesAsync();
    }
}
