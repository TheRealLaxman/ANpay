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
            ErrorMessage = errorMessage
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
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
}
