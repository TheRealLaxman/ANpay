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
    private const int RAPID_TRANSFER_COUNT = 5;
    private const int RAPID_TRANSFER_MINUTES = 10;

    public FraudService(ApplicationDbContext context, ILogger<FraudService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CalculateRiskScoreAsync(string userId, decimal amount, string ipAddress, string deviceInfo)
    {
        int score = 0;

        var recentTxCount = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt > DateTime.UtcNow.AddHours(-1))
            .CountAsync();
        if (recentTxCount > VELOCITY_LIMIT_PER_HOUR)
            score += 30;

        var dailyTxCount = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt > DateTime.UtcNow.AddDays(-1))
            .CountAsync();
        if (dailyTxCount > VELOCITY_LIMIT_PER_DAY)
            score += 20;

        if (amount > LARGE_TRANSACTION_THRESHOLD)
            score += 25;

        var rapidTransfers = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId
                && t.Type == TransactionType.TransferOut
                && t.CreatedAt > DateTime.UtcNow.AddMinutes(-RAPID_TRANSFER_MINUTES))
            .CountAsync();
        if (rapidTransfers >= RAPID_TRANSFER_COUNT)
            score += 30;

        var knownIP = await _context.LoginHistories
            .Where(l => l.UserId == userId && l.IPAddress == ipAddress && l.IsSuccess)
            .AnyAsync();
        if (!knownIP)
            score += 15;

        var failedLogins = await _context.LoginHistories
            .Where(l => l.UserId == userId && !l.IsSuccess && l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .CountAsync();
        if (failedLogins > 3)
            score += 20;

        var knownDevice = await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.DeviceInfo == deviceInfo && d.IsActive)
            .AnyAsync();
        if (!knownDevice)
            score += 10;

        score = Math.Min(score, 100);

        var riskScore = new RiskScore
        {
            EntityType = "User",
            EntityId = userId,
            Score = score,
            Factors = $"Velocity:{recentTxCount}/hr, Daily:{dailyTxCount}, Amount:{amount}, KnownIP:{knownIP}, FailedLogins:{failedLogins}",
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

        if (score >= 40)
        {
            await CreateAlertAsync(userId, FraudAlertType.HighRiskTransaction, score,
                $"High risk transaction detected. Score: {score}. Amount: {amount}. IP: {ipAddress}",
                ipAddress, deviceInfo);
        }

        return score;
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
