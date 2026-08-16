using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class LoyaltyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoyaltyService> _logger;

    public LoyaltyService(ApplicationDbContext context, ILogger<LoyaltyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoyaltyPoint> GetOrCreateLoyaltyPointsAsync(string userId)
    {
        var existing = await _context.LoyaltyPoints.FirstOrDefaultAsync(lp => lp.UserId == userId);
        if (existing != null) return existing;

        var loyaltyPoint = new LoyaltyPoint
        {
            UserId = userId,
            TotalPoints = 0,
            UsedPoints = 0,
            LifetimePoints = 0,
            Tier = LoyaltyTier.Bronze
        };

        _context.LoyaltyPoints.Add(loyaltyPoint);
        await _context.SaveChangesAsync();
        return loyaltyPoint;
    }

    public async Task<LoyaltyPoint> EarnPointsAsync(string userId, int points, LoyaltyTransactionType type, string description, Guid? transactionId = null, Guid? walletId = null)
    {
        var loyaltyPoint = await GetOrCreateLoyaltyPointsAsync(userId);

        // Tier multiplier
        var multiplier = loyaltyPoint.Tier switch
        {
            LoyaltyTier.Bronze => 1.0m,
            LoyaltyTier.Silver => 1.2m,
            LoyaltyTier.Gold => 1.5m,
            LoyaltyTier.Platinum => 2.0m,
            LoyaltyTier.Diamond => 3.0m,
            _ => 1.0m
        };

        var adjustedPoints = (int)(points * multiplier);

        loyaltyPoint.TotalPoints += adjustedPoints;
        loyaltyPoint.LifetimePoints += adjustedPoints;

        // Update tier based on lifetime points
        loyaltyPoint.Tier = loyaltyPoint.LifetimePoints switch
        {
            >= 100000 => LoyaltyTier.Diamond,
            >= 50000 => LoyaltyTier.Platinum,
            >= 20000 => LoyaltyTier.Gold,
            >= 5000 => LoyaltyTier.Silver,
            _ => LoyaltyTier.Bronze
        };

        var loyaltyTx = new LoyaltyTransaction
        {
            UserId = userId,
            Type = type,
            Points = adjustedPoints,
            Description = description,
            TransactionId = transactionId,
            WalletId = walletId
        };

        _context.LoyaltyTransactions.Add(loyaltyTx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} earned {Points} loyalty points (adjusted: {AdjustedPoints})", userId, points, adjustedPoints);
        return loyaltyPoint;
    }

    public async Task<LoyaltyPoint> RedeemPointsAsync(string userId, int points, string description)
    {
        var loyaltyPoint = await GetOrCreateLoyaltyPointsAsync(userId);

        if (loyaltyPoint.AvailablePoints < points)
            throw new ValidationException("Insufficient loyalty points");

        loyaltyPoint.UsedPoints += points;

        var loyaltyTx = new LoyaltyTransaction
        {
            UserId = userId,
            Type = LoyaltyTransactionType.Redeemed,
            Points = -points,
            Description = description
        };

        _context.LoyaltyTransactions.Add(loyaltyTx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} redeemed {Points} loyalty points", userId, points);
        return loyaltyPoint;
    }

    public async Task<List<LoyaltyTransaction>> GetTransactionHistoryAsync(string userId, int page = 1, int pageSize = 20)
    {
        return await _context.LoyaltyTransactions
            .Where(lt => lt.UserId == userId)
            .OrderByDescending(lt => lt.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Cashback> ProcessCashbackAsync(string userId, Guid walletId, Guid transactionId, decimal amount, CashbackType type)
    {
        // Determine cashback percentage based on type
        var percentage = type switch
        {
            CashbackType.Transaction => 1.0m,
            CashbackType.Referral => 5.0m,
            CashbackType.Promotion => 2.5m,
            CashbackType.Bonus => 10.0m,
            _ => 0.5m
        };

        var cashbackAmount = amount * percentage / 100;

        var cashback = new Cashback
        {
            UserId = userId,
            WalletId = walletId,
            TransactionId = transactionId,
            OriginalAmount = amount,
            CashbackAmount = cashbackAmount,
            CashbackPercentage = percentage,
            Status = CashbackStatus.Credited,
            Type = type
        };

        _context.Cashbacks.Add(cashback);

        // Credit the wallet
        var wallet = await _context.Wallets.FindAsync(walletId);
        if (wallet != null)
        {
            wallet.Balance += cashbackAmount;

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Refund,
                Amount = cashbackAmount,
                BalanceBefore = wallet.Balance - cashbackAmount,
                BalanceAfter = wallet.Balance,
                Description = $"Cashback reward ({percentage}%)",
                ReferenceNumber = $"CB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
        }

        await _context.SaveChangesAsync();
        return cashback;
    }

    public async Task<Referral> ProcessReferralAsync(string referrerUserId, string referredUserId)
    {
        var referral = new Referral
        {
            ReferrerUserId = referrerUserId,
            ReferredUserId = referredUserId,
            Status = ReferralStatus.Completed,
            ReferrerRewardPoints = 1000,
            ReferredRewardPoints = 500,
            ReferrerCashReward = 500,
            CompletedAt = DateTime.UtcNow
        };

        _context.Referrals.Add(referral);

        // Award points to referrer
        await EarnPointsAsync(referrerUserId, 1000, LoyaltyTransactionType.Referral, "Referral bonus for inviting a friend");

        // Award points to referred user
        await EarnPointsAsync(referredUserId, 500, LoyaltyTransactionType.Bonus, "Welcome bonus for joining via referral");

        await _context.SaveChangesAsync();
        return referral;
    }

    public async Task<List<Referral>> GetUserReferralsAsync(string userId)
    {
        return await _context.Referrals
            .Where(r => r.ReferrerUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}