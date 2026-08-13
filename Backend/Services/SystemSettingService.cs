using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class SystemSettingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SystemSettingService> _logger;

    public SystemSettingService(ApplicationDbContext context, ILogger<SystemSettingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        return await _context.SystemSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync();
    }

    public async Task<List<SystemSetting>> GetByCategoryAsync(string category)
    {
        return await _context.SystemSettings
            .Where(s => s.Category == category)
            .OrderBy(s => s.Key)
            .ToListAsync();
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        return await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key);
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    public async Task<SystemSetting> SetAsync(string key, string value, string category = "General", string? description = null)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                Category = category,
                Description = description
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.Category = category;
            if (description != null)
                setting.Description = description;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return setting;
    }

    public async Task DeleteAsync(string key)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key)
            ?? throw new NotFoundException($"Setting '{key}' not found");

        _context.SystemSettings.Remove(setting);
        await _context.SaveChangesAsync();
    }
}
