namespace GymForYou.Api.Services;

public class RenewalReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RenewalReminderBackgroundService> _logger;
    private readonly int _runHourUtc;

    public RenewalReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RenewalReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _runHourUtc = ParseRunHour(configuration["RENEWAL_REMINDER_RUN_HOUR_UTC"]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Renewal reminder background service started (hour UTC: {Hour})", _runHourUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = new DateTime(now.Year, now.Month, now.Day, _runHourUtc, 0, 0, DateTimeKind.Utc);
            if (next <= now) next = next.AddDays(1);
            var delay = next - now;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IRenewalReminderService>();
                var sent = await reminders.SendForAllTenantsAsync(stoppingToken);
                _logger.LogInformation("Daily renewal reminder run completed. Sent={Sent}", sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Renewal reminder run failed");
            }
        }
    }

    private static int ParseRunHour(string? value)
    {
        if (int.TryParse(value, out var hour) && hour >= 0 && hour <= 23) return hour;
        return 7;
    }
}

