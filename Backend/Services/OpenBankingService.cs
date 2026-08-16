using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class OpenBankingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OpenBankingService> _logger;

    public OpenBankingService(ApplicationDbContext context, ILogger<OpenBankingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiKey> CreateApiKeyAsync(string userId, string name, ApiKeyScope scope, int maxUsagePerDay = 1000, int expiryDays = 365)
    {
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = name,
            Key = GenerateApiKey(),
            Secret = GenerateApiKey(),
            Scope = scope,
            MaxUsagePerDay = maxUsagePerDay,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();
        return apiKey;
    }

    public async Task<List<ApiKey>> GetUserApiKeysAsync(string userId)
    {
        return await _context.ApiKeys.Where(ak => ak.UserId == userId).ToListAsync();
    }

    public async Task<ApiKey> RevokeApiKeyAsync(Guid keyId, string userId)
    {
        var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(ak => ak.Id == keyId && ak.UserId == userId);
        if (apiKey == null) throw new NotFoundException("API key not found");
        apiKey.IsActive = false;
        await _context.SaveChangesAsync();
        return apiKey;
    }

    public async Task<bool> ValidateApiKeyAsync(string key)
    {
        var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(ak => ak.Key == key && ak.IsActive);
        if (apiKey == null) return false;
        if (apiKey.ExpiresAt < DateTime.UtcNow) return false;
        if (apiKey.UsageCount >= apiKey.MaxUsagePerDay) return false;

        apiKey.UsageCount++;
        apiKey.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Webhook> CreateWebhookAsync(string userId, string url, string name, string? events = null)
    {
        var webhook = new Webhook
        {
            UserId = userId,
            Url = url,
            Name = name,
            Secret = GenerateApiKey(),
            Events = events
        };

        _context.Webhooks.Add(webhook);
        await _context.SaveChangesAsync();
        return webhook;
    }

    public async Task<List<Webhook>> GetUserWebhooksAsync(string userId)
    {
        return await _context.Webhooks.Where(w => w.UserId == userId).ToListAsync();
    }

    public async Task<Webhook> ToggleWebhookAsync(Guid webhookId, string userId)
    {
        var webhook = await _context.Webhooks.FirstOrDefaultAsync(w => w.Id == webhookId && w.UserId == userId);
        if (webhook == null) throw new NotFoundException("Webhook not found");
        webhook.IsActive = !webhook.IsActive;
        await _context.SaveChangesAsync();
        return webhook;
    }

    public async Task<OpenBankingAccount> ConnectBankAccountAsync(string userId, string bankName, string bankCode, string accountNumber, string accountName, AccountType accountType, string currency = "NGN")
    {
        var account = new OpenBankingAccount
        {
            UserId = userId,
            BankName = bankName,
            BankCode = bankCode,
            AccountNumber = accountNumber,
            AccountName = accountName,
            AccountType = accountType,
            Currency = currency,
            Status = OpenBankingAccountStatus.Connected
        };

        _context.OpenBankingAccounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<List<OpenBankingAccount>> GetUserBankAccountsAsync(string userId)
    {
        return await _context.OpenBankingAccounts.Where(oa => oa.UserId == userId).ToListAsync();
    }

    public async Task<OpenBankingAccount> DisconnectBankAccountAsync(Guid accountId, string userId)
    {
        var account = await _context.OpenBankingAccounts.FirstOrDefaultAsync(oa => oa.Id == accountId && oa.UserId == userId);
        if (account == null) throw new NotFoundException("Bank account not found");
        account.Status = OpenBankingAccountStatus.Disconnected;
        await _context.SaveChangesAsync();
        return account;
    }

    private string GenerateApiKey()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Replace("-", "")[..32];
    }
}
