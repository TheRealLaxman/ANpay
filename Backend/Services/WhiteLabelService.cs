using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class WhiteLabelService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WhiteLabelService> _logger;

    public WhiteLabelService(ApplicationDbContext context, ILogger<WhiteLabelService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WhiteLabelTenant> CreateTenantAsync(string companyName, string contactEmail, string? contactPhone, WhiteLabelPlan plan, int maxUsers = 1000)
    {
        var tenant = new WhiteLabelTenant
        {
            CompanyName = companyName,
            TenantCode = GenerateTenantCode(),
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            Plan = plan,
            MaxUsers = maxUsers,
            Status = WhiteLabelStatus.Trial,
            ApiKey = Guid.NewGuid().ToString("N"),
            WebhookSecret = Guid.NewGuid().ToString("N")
        };

        _context.WhiteLabelTenants.Add(tenant);
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<List<WhiteLabelTenant>> GetAllTenantsAsync()
    {
        return await _context.WhiteLabelTenants.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<WhiteLabelTenant?> GetTenantByIdAsync(Guid id)
    {
        return await _context.WhiteLabelTenants.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<WhiteLabelTenant> ActivateTenantAsync(Guid tenantId)
    {
        var tenant = await _context.WhiteLabelTenants.FindAsync(tenantId);
        if (tenant == null) throw new NotFoundException("Tenant not found");
        tenant.Status = WhiteLabelStatus.Active;
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<WhiteLabelTenant> SuspendTenantAsync(Guid tenantId)
    {
        var tenant = await _context.WhiteLabelTenants.FindAsync(tenantId);
        if (tenant == null) throw new NotFoundException("Tenant not found");
        tenant.Status = WhiteLabelStatus.Suspended;
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<TenantUser> AddUserToTenantAsync(Guid tenantId, string userId, TenantUserRole role = TenantUserRole.User)
    {
        var tenant = await _context.WhiteLabelTenants.FindAsync(tenantId);
        if (tenant == null) throw new NotFoundException("Tenant not found");

        if (tenant.CurrentUsers >= tenant.MaxUsers)
            throw new ValidationException("Maximum user limit reached");

        var tenantUser = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        _context.TenantUsers.Add(tenantUser);
        tenant.CurrentUsers++;
        await _context.SaveChangesAsync();

        return tenantUser;
    }

    public async Task<List<TenantUser>> GetTenantUsersAsync(Guid tenantId)
    {
        return await _context.TenantUsers
            .Where(tu => tu.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<WhiteLabelTenant> UpdateTenantSettingsAsync(Guid tenantId, string? logoUrl, string? primaryColor, string? secondaryColor, string? customDomain, decimal? transactionFeePercentage)
    {
        var tenant = await _context.WhiteLabelTenants.FindAsync(tenantId);
        if (tenant == null) throw new NotFoundException("Tenant not found");

        if (logoUrl != null) tenant.LogoUrl = logoUrl;
        if (primaryColor != null) tenant.PrimaryColor = primaryColor;
        if (secondaryColor != null) tenant.SecondaryColor = secondaryColor;
        if (customDomain != null) tenant.CustomDomain = customDomain;
        if (transactionFeePercentage.HasValue) tenant.TransactionFeePercentage = transactionFeePercentage.Value;

        await _context.SaveChangesAsync();
        return tenant;
    }

    private string GenerateTenantCode()
    {
        return $"TN{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }
}
