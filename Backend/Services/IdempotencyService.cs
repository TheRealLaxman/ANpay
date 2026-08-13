using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class IdempotencyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(ApplicationDbContext context, ILogger<IdempotencyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IdempotencyKey?> GetExistingRequestAsync(string key, string userId)
    {
        var record = await _context.IdempotencyKeys
            .FirstOrDefaultAsync(ik => ik.Key == key && ik.UserId == userId);

        if (record == null) return null;

        if (record.ExpiresAt < DateTime.UtcNow)
        {
            _context.IdempotencyKeys.Remove(record);
            await _context.SaveChangesAsync();
            return null;
        }

        return record;
    }

    public async Task StoreRequestAsync(string key, string userId, string endpoint, string? requestBody, string? responseBody, int statusCode, TimeSpan? ttl = null)
    {
        var expiry = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromHours(24));

        var record = new IdempotencyKey
        {
            Key = key,
            UserId = userId,
            Endpoint = endpoint,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            StatusCode = statusCode,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiry
        };

        _context.IdempotencyKeys.Add(record);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Stored idempotency record for key {Key}, endpoint {Endpoint}", key, endpoint);
    }

    public async Task CleanupExpiredKeysAsync()
    {
        var expiredKeys = await _context.IdempotencyKeys
            .Where(ik => ik.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        if (expiredKeys.Count > 0)
        {
            _context.IdempotencyKeys.RemoveRange(expiredKeys);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired idempotency keys", expiredKeys.Count);
        }
    }

    public async Task<int> GetActiveKeyCountAsync()
    {
        return await _context.IdempotencyKeys
            .CountAsync(ik => ik.ExpiresAt >= DateTime.UtcNow);
    }
}
