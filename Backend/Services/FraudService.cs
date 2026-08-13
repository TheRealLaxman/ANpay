using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class FraudService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FraudService> _logger;

    private const int VELOCITY_LIMIT_PER_HOUR = 10;
    private const int VELOCITY_LIMIT_PER_DAY = 50;
    private const decimal LARGE_TRANSACTION_THRESHOLD = 10000m;
    private const decimal VERY_LARGE_TRANSACTION_THRESHOLD = 50000m;
    private const int RAPID_TRANSFER_COUNT = 5;
    private const int RAPID_TRANSFER_MINUTES = 10;
    private const int MAX_FAILED_LOGINS_24H = 3;
    private const int MAX_DIFFERENT_IPS_24H = 3;
    private const decimal DAILY_CUMULATIVE_LIMIT = 100000m;

    public FraudService(ApplicationDbContext context, ILogger<FraudService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CalculateRiskScoreAsync(string userId, decimal amount, string ipAddress, string deviceInfo)
    {
        int score = 0;
        var factors = new List<string>();

        // Rule 1: Transaction velocity (per hour)
        var recentTxCount = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt > DateTime.UtcNow.AddHours(-1))
            .CountAsync();
        if (recentTxCount > VELOCITY_LIMIT_PER_HOUR)
        {
            score += 30;
            factors.Add($"HighVelocity:{recentTxCount}/hr");
        }

        // Rule 2: Daily transaction count
        var dailyTxCount = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt > DateTime.UtcNow.AddDays(-1))
            .CountAsync();
        if (dailyTxCount > VELOCITY_LIMIT_PER_DAY)
        {
            score += 20;
            factors.Add($"DailyLimit:{dailyTxCount}");
        }

        // Rule 3: Large single transaction
        if (amount > VERY_LARGE_TRANSACTION_THRESHOLD)
        {
            score += 35;
            factors.Add($"VeryLargeAmount:{amount}");
        }
        else if (amount > LARGE_TRANSACTION_THRESHOLD)
        {
            score += 25;
            factors.Add($"LargeAmount:{amount}");
        }

        // Rule 4: Rapid successive transfers
        var rapidTransfers = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId
                && t.Type == TransactionType.TransferOut
                && t.CreatedAt > DateTime.UtcNow.AddMinutes(-RAPID_TRANSFER_MINUTES))
            .CountAsync();
        if (rapidTransfers >= RAPID_TRANSFER_COUNT)
        {
            score += 30;
            factors.Add($"RapidTransfers:{rapidTransfers}in{RAPID_TRANSFER_MINUTES}min");
        }

        // Rule 5: Daily cumulative amount threshold
        var dailyCumulative = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId
                && (t.Type == TransactionType.TransferOut || t.Type == TransactionType.Withdrawal)
                && t.CreatedAt > DateTime.UtcNow.AddDays(-1)
                && t.Status == TransactionStatus.Completed)
            .SumAsync(t => t.Amount);
        if (dailyCumulative + amount > DAILY_CUMULATIVE_LIMIT)
        {
            score += 25;
            factors.Add($"DailyCumulative:{dailyCumulative + amount}");
        }

        // Rule 6: Unknown IP address
        var knownIP = await _context.LoginHistories
            .Where(l => l.UserId == userId && l.IPAddress == ipAddress && l.IsSuccess)
            .AnyAsync();
        if (!knownIP)
        {
            score += 15;
            factors.Add("UnknownIP");
        }

        // Rule 7: Multiple different IPs in 24h (potential account sharing or compromise)
        var differentIPs = await _context.LoginHistories
            .Where(l => l.UserId == userId && l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .Select(l => l.IPAddress)
            .Distinct()
            .CountAsync();
        if (differentIPs > MAX_DIFFERENT_IPS_24H)
        {
            score += 20;
            factors.Add($"MultipleIPs:{differentIPs}");
        }

        // Rule 8: Recent failed logins
        var failedLogins = await _context.LoginHistories
            .Where(l => l.UserId == userId && !l.IsSuccess && l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .CountAsync();
        if (failedLogins > MAX_FAILED_LOGINS_24H)
        {
            score += 20;
            factors.Add($"FailedLogins:{failedLogins}");
        }

        // Rule 9: Unknown device
        var knownDevice = await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.DeviceInfo == deviceInfo && d.IsActive)
            .AnyAsync();
        if (!knownDevice)
        {
            score += 10;
            factors.Add("UnknownDevice");
        }

        // Rule 10: Unusual hour (2am - 5am)
        if (DateTime.UtcNow.Hour >= 2 && DateTime.UtcNow.Hour <= 5)
        {
            score += 10;
            factors.Add("UnusualHour");
        }

        score = Math.Min(score, 100);

        var riskScore = new RiskScore
        {
            EntityType = "User",
            EntityId = userId,
            Score = score,
            Factors = string.Join(", ", factors),
            Level = score switch
            {
                >= 70 => RiskLevel.Critical,
                >= 40 => RiskLevel.High,
                >= 20 => RiskLevel.Medium,
                _ => RiskLevel.Low
            }
        };

        _context.RiskScores.Add(riskScore);
        await _context.SaveChangesAsync();

        if (score >= 70)
        {
            await CreateAlertAsync(userId, FraudAlertType.HighRiskTransaction, score,
                $"CRITICAL risk transaction. Score: {score}. Amount: {amount}. Factors: {string.Join(", ", factors)}",
                ipAddress, deviceInfo);
        }
        else if (score >= 40)
        {
            await CreateAlertAsync(userId, FraudAlertType.SuspiciousLogin, score,
                $"High risk transaction. Score: {score}. Amount: {amount}. Factors: {string.Join(", ", factors)}",
                ipAddress, deviceInfo);
        }

        return score;
    }

    public async Task<List<FraudAlert>> RunPeriodicChecksAsync()
    {
        var alerts = new List<FraudAlert>();

        // Check for users with unusually high daily transaction counts
        var highVolumeUsers = await _context.Transactions
            .Where(t => t.CreatedAt > DateTime.UtcNow.AddDays(-1) && t.Status == TransactionStatus.Completed)
            .GroupBy(t => t.Wallet.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Total = g.Sum(t => t.Amount) })
            .Where(x => x.Count > 100 || x.Total > 200000)
            .ToListAsync();

        foreach (var user in highVolumeUsers)
        {
            var existing = await _context.FraudAlerts
                .Where(a => a.UserId == user.UserId && a.AlertType == FraudAlertType.HighRiskTransaction
                    && a.CreatedAt > DateTime.UtcNow.AddHours(-24))
                .AnyAsync();

            if (!existing)
            {
                alerts.Add(await CreateAlertAsync(user.UserId, FraudAlertType.HighRiskTransaction, 80,
                    $"Abnormal daily volume: {user.Count} transactions totaling {user.Total}"));
            }
        }

        // Check for accounts with multiple failed logins followed by success (potential brute force)
        var suspiciousLogins = await _context.LoginHistories
            .Where(l => l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .GroupBy(l => l.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Failed = g.Count(l => !l.IsSuccess),
                Success = g.Count(l => l.IsSuccess)
            })
            .Where(x => x.Failed >= 5 && x.Success > 0)
            .ToListAsync();

        foreach (var login in suspiciousLogins)
        {
            var existing = await _context.FraudAlerts
                .Where(a => a.UserId == login.UserId && a.AlertType == FraudAlertType.SuspiciousLogin
                    && a.CreatedAt > DateTime.UtcNow.AddHours(-24))
                .AnyAsync();

            if (!existing)
            {
                alerts.Add(await CreateAlertAsync(login.UserId, FraudAlertType.SuspiciousLogin, 75,
                    $"Possible brute force: {login.Failed} failed then {login.Success} successful logins in 24h"));
            }
        }

        return alerts;
    }

    public async Task<FraudAlert> CreateAlertAsync(string userId, FraudAlertType type, int riskScore,
        string description, string? ipAddress = null, string? deviceInfo = null)
    {
        var alert = new FraudAlert
        {
            UserId = userId,
            AlertType = type,
            RiskScore = riskScore,
            Description = description,
            IPAddress = ipAddress,
            DeviceInfo = deviceInfo,
            Status = FraudAlertStatus.Open
        };

        _context.FraudAlerts.Add(alert);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Fraud alert created for user {UserId}: {Type} (Score: {Score})",
            userId, type, riskScore);

        return alert;
    }

    public async Task<List<FraudAlert>> GetOpenAlertsAsync()
    {
        return await _context.FraudAlerts
            .Where(a => a.Status == FraudAlertStatus.Open || a.Status == FraudAlertStatus.UnderReview)
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<FraudAlert?> GetAlertByIdAsync(Guid id)
    {
        return await _context.FraudAlerts
            .Include(a => a.User)
            .Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task UpdateAlertStatusAsync(Guid alertId, FraudAlertStatus status, string? resolution = null)
    {
        var alert = await _context.FraudAlerts.FindAsync(alertId)
            ?? throw new NotFoundException("Fraud alert not found");

        alert.Status = status;
        alert.Resolution = resolution;

        if (status == FraudAlertStatus.Resolved || status == FraudAlertStatus.FalsePositive)
            alert.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task AssignAlertAsync(Guid alertId, string assignedToId)
    {
        var alert = await _context.FraudAlerts.FindAsync(alertId)
            ?? throw new NotFoundException("Fraud alert not found");

        alert.AssignedToId = assignedToId;
        alert.Status = FraudAlertStatus.UnderReview;
        await _context.SaveChangesAsync();
    }
}
