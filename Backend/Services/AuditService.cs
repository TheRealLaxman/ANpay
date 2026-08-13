using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class AuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string? userId, string action, string entity, Guid? entityId,
        string oldValues = "", string newValues = "", string ipAddress = "", string userAgent = "",
        bool isSuccess = true, string errorMessage = "")
    {
        var previousHash = await GetLatestHashAsync();

        var data = $"{userId}|{action}|{entity}|{entityId}|{oldValues}|{newValues}|{ipAddress}|{isSuccess}|{DateTime.UtcNow.Ticks}";
        var hash = ComputeHash(data, previousHash);

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            Hash = hash,
            PreviousHash = previousHash
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> VerifyIntegrityAsync()
    {
        var logs = await _context.AuditLogs
            .OrderBy(al => al.CreatedAt)
            .ToListAsync();

        string? previousHash = null;
        foreach (var log in logs)
        {
            if (log.PreviousHash != previousHash)
            {
                _logger.LogWarning("Audit log integrity breach at {Id}: expected PreviousHash {Expected}, got {Actual}",
                    log.Id, previousHash, log.PreviousHash);
                return false;
            }

            var data = $"{log.UserId}|{log.Action}|{log.Entity}|{log.EntityId}|{log.OldValues}|{log.NewValues}|{log.IpAddress}|{log.IsSuccess}|{log.CreatedAt.Ticks}";
            var expectedHash = ComputeHash(data, log.PreviousHash);
            if (log.Hash != expectedHash)
            {
                _logger.LogWarning("Audit log hash mismatch at {Id}", log.Id);
                return false;
            }

            previousHash = log.Hash;
        }

        return true;
    }

    public async Task<List<AuditLog>> GetAsync(string? userId = null, string? action = null,
        DateTime? from = null, DateTime? to = null, int skip = 0, int take = 100)
    {
        var query = _context.AuditLogs.Include(al => al.User).AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(al => al.UserId == userId);
        if (!string.IsNullOrEmpty(action))
            query = query.Where(al => al.Action == action);
        if (from.HasValue)
            query = query.Where(al => al.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(al => al.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(al => al.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    private async Task<string?> GetLatestHashAsync()
    {
        return await _context.AuditLogs
            .OrderByDescending(al => al.CreatedAt)
            .Select(al => al.Hash)
            .FirstOrDefaultAsync();
    }

    private static string ComputeHash(string data, string? previousHash)
    {
        var input = $"{previousHash ?? ""}|{data}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
