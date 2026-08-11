using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class LimitService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LimitService> _logger;

    public LimitService(ApplicationDbContext context, ILogger<LimitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TransactionLimit>> GetAllAsync()
    {
        return await _context.TransactionLimits
            .OrderBy(tl => tl.RoleName)
            .ThenBy(tl => tl.LimitType)
            .ToListAsync();
    }

    public async Task<TransactionLimit> CreateAsync(TransactionLimit limit)
    {
        if (await _context.TransactionLimits.AnyAsync(tl => tl.RoleName == limit.RoleName && tl.LimitType == limit.LimitType))
        {
            var existing = await _context.TransactionLimits.FirstAsync(tl => tl.RoleName == limit.RoleName && tl.LimitType == limit.LimitType);
            existing.LimitAmount = limit.LimitAmount;
            existing.Currency = limit.Currency;
            existing.IsActive = limit.IsActive;
            await _context.SaveChangesAsync();
            return existing;
        }

        _context.TransactionLimits.Add(limit);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Limit created: {Role} {Type} = {Amount}", limit.RoleName, limit.LimitType, limit.LimitAmount);
        return limit;
    }

    public async Task<TransactionLimit> UpdateAsync(Guid id, decimal limitAmount)
    {
        var limit = await _context.TransactionLimits.FindAsync(id)
            ?? throw new Exceptions.NotFoundException("Limit not found");

        limit.LimitAmount = limitAmount;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Limit updated: {Id} to {Amount}", id, limitAmount);
        return limit;
    }

    public async Task<bool> CheckLimitAsync(string userId, TransactionLimitType type, decimal amount, string currency = "NGN")
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        var role = ((AppUserRole)((int)user.Role)).ToString();
        var limit = await _context.TransactionLimits
            .FirstOrDefaultAsync(tl => tl.RoleName == role && tl.LimitType == type && tl.Currency == currency && tl.IsActive);

        if (limit == null) return true;

        var now = DateTime.UtcNow;
        var startDate = type switch
        {
            TransactionLimitType.DailyDeposit or TransactionLimitType.DailyWithdrawal or TransactionLimitType.DailyTransfer => now.Date,
            TransactionLimitType.MonthlyDeposit or TransactionLimitType.MonthlyWithdrawal or TransactionLimitType.MonthlyTransfer => new DateTime(now.Year, now.Month, 1),
            TransactionLimitType.SingleTransaction => DateTime.MinValue,
            _ => now.Date
        };

        if (type == TransactionLimitType.SingleTransaction)
            return amount <= limit.LimitAmount;

        var transactions = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt >= startDate && t.Status == TransactionStatus.Completed)
            .ToListAsync();

        var totalAmount = type switch
        {
            TransactionLimitType.DailyDeposit or TransactionLimitType.MonthlyDeposit =>
                transactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.Amount),
            TransactionLimitType.DailyWithdrawal or TransactionLimitType.MonthlyWithdrawal =>
                transactions.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.Amount),
            TransactionLimitType.DailyTransfer or TransactionLimitType.MonthlyTransfer =>
                transactions.Where(t => t.Type == TransactionType.TransferOut).Sum(t => t.Amount),
            _ => 0
        };

        return (totalAmount + amount) <= limit.LimitAmount;
    }

    public async Task SeedDefaultLimitsAsync()
    {
        if (await _context.TransactionLimits.AnyAsync()) return;

        var limits = new List<TransactionLimit>
        {
            new() { RoleName = "Customer", LimitType = TransactionLimitType.DailyDeposit, LimitAmount = 500000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.DailyWithdrawal, LimitAmount = 200000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.DailyTransfer, LimitAmount = 300000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.MonthlyDeposit, LimitAmount = 5000000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.MonthlyWithdrawal, LimitAmount = 2000000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.MonthlyTransfer, LimitAmount = 3000000 },
            new() { RoleName = "Customer", LimitType = TransactionLimitType.SingleTransaction, LimitAmount = 100000 },

            new() { RoleName = "Official", LimitType = TransactionLimitType.DailyDeposit, LimitAmount = 5000000 },
            new() { RoleName = "Official", LimitType = TransactionLimitType.DailyWithdrawal, LimitAmount = 2000000 },
            new() { RoleName = "Official", LimitType = TransactionLimitType.DailyTransfer, LimitAmount = 3000000 },
            new() { RoleName = "Official", LimitType = TransactionLimitType.SingleTransaction, LimitAmount = 1000000 },

            new() { RoleName = "BranchAdmin", LimitType = TransactionLimitType.DailyDeposit, LimitAmount = 50000000 },
            new() { RoleName = "BranchAdmin", LimitType = TransactionLimitType.DailyWithdrawal, LimitAmount = 20000000 },
            new() { RoleName = "BranchAdmin", LimitType = TransactionLimitType.DailyTransfer, LimitAmount = 30000000 },
            new() { RoleName = "BranchAdmin", LimitType = TransactionLimitType.SingleTransaction, LimitAmount = 10000000 },
        };

        _context.TransactionLimits.AddRange(limits);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Default transaction limits seeded");
    }
}
