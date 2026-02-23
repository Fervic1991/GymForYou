using GymForYou.Api.Data;
using GymForYou.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GymForYou.Api.Services;

public interface ITenantSettingsService
{
    Task<TenantSettings> GetOrCreateAsync();
}

public class TenantSettingsService : ITenantSettingsService
{
    private readonly AppDbContext _db;

    public TenantSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TenantSettings> GetOrCreateAsync()
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync();
        if (settings is not null) return settings;

        settings = new TenantSettings();
        _db.TenantSettings.Add(settings);
        await _db.SaveChangesAsync();
        return settings;
    }
}
