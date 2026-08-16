using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class CreditScoreService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CreditScoreService> _logger;

    public CreditScoreService(ApplicationDbContext context, ILogger<CreditScoreService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreditScore> GetOrCreateCreditScoreAsync(string userId)
    {
        var existing = await _context.CreditScores.FirstOrDefaultAsync(cs => cs.UserId == userId);
        if (existing != null) return existing;

        var creditScore = new CreditScore
        {
            UserId = userId,
            Score = 300,
            Rating = CreditRating.New,
            MaximumCreditLimit = 0,
            InterestRate = 25
        };

        _context.CreditScores.Add(creditScore);
        await _context.SaveChangesAsync();
        return creditScore;
    }

    public async Task<CreditScore> CalculateCreditScoreAsync(string userId)
    {
        var creditScore = await GetOrCreateCreditScoreAsync(userId);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User not found");

        var factors = new List<CreditScoreFactor>();
        int baseScore = 300;

        // Factor 1: Account age (max 100 points)
        var accountAgeDays = (DateTime.UtcNow - user.CreatedAt).Days;
        var accountAgeScore = Math.Min(100, accountAgeDays / 3);
        baseScore += accountAgeScore;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "Account Age",
            Weight = 15,
            Impact = accountAgeScore,
            Description = $"Account is {accountAgeDays} days old"
        });

        // Factor 2: Transaction volume (max 150 points)
        var totalTransactions = await _context.Transactions
            .CountAsync(t => t.Wallet.UserId == userId && t.Status == TransactionStatus.Completed);
        var transactionScore = Math.Min(150, totalTransactions * 2);
        baseScore += transactionScore;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "Transaction Volume",
            Weight = 20,
            Impact = transactionScore,
            Description = $"{totalTransactions} completed transactions"
        });

        // Factor 3: Total volume (max 100 points)
        var totalVolume = await _context.Transactions
            .Where(t => t.Wallet.UserId == userId && t.Status == TransactionStatus.Completed)
            .SumAsync(t => t.Amount);
        var volumeScore = Math.Min(100, (int)(totalVolume / 1000));
        baseScore += volumeScore;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "Transaction Volume (Value)",
            Weight = 15,
            Impact = volumeScore,
            Description = $"Total volume: {totalVolume:N2}"
        });

        // Factor 4: KYC status (max 100 points)
        var kyc = await _context.KycProfiles.FirstOrDefaultAsync(kp => kp.UserId == userId);
        var kycScore = kyc?.Status == KycStatus.Approved ? 100 : kyc?.Status == KycStatus.Submitted ? 50 : 0;
        baseScore += kycScore;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "KYC Verification",
            Weight = 15,
            Impact = kycScore,
            Description = kyc?.Status.ToString() ?? "Not submitted"
        });

        // Factor 5: Dispute history (penalty)
        var disputes = await _context.Disputes.CountAsync(d => d.UserId == userId);
        var disputePenalty = Math.Min(50, disputes * 10);
        baseScore -= disputePenalty;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "Disputes",
            Weight = 10,
            Impact = -disputePenalty,
            Description = $"{disputes} disputes filed"
        });

        // Factor 6: Loan repayment history (if applicable)
        var completedLoans = await _context.Microloans.CountAsync(ml => ml.UserId == userId && ml.Status == MicroloanStatus.Completed);
        var defaultedLoans = await _context.Microloans.CountAsync(ml => ml.UserId == userId && ml.Status == MicroloanStatus.Defaulted);
        var loanScore = (completedLoans * 20) - (defaultedLoans * 50);
        baseScore += loanScore;
        factors.Add(new CreditScoreFactor
        {
            CreditScoreId = creditScore.Id,
            FactorName = "Loan History",
            Weight = 15,
            Impact = loanScore,
            Description = $"{completedLoans} completed, {defaultedLoans} defaulted"
        });

        // Clamp score to 300-850
        baseScore = Math.Max(300, Math.Min(850, baseScore));

        // Determine rating
        var rating = baseScore switch
        {
            >= 800 => CreditRating.Exceptional,
            >= 740 => CreditRating.VeryGood,
            >= 670 => CreditRating.Good,
            >= 580 => CreditRating.Fair,
            _ => CreditRating.Poor
        };

        // Calculate credit limit based on score
        var creditLimit = rating switch
        {
            CreditRating.Exceptional => 5000000,
            CreditRating.VeryGood => 2000000,
            CreditRating.Good => 1000000,
            CreditRating.Fair => 500000,
            CreditRating.Poor => 100000,
            _ => 0
        };

        // Interest rate based on rating
        var interestRate = rating switch
        {
            CreditRating.Exceptional => 8,
            CreditRating.VeryGood => 12,
            CreditRating.Good => 18,
            CreditRating.Fair => 24,
            CreditRating.Poor => 30,
            _ => 35
        };

        // Update credit score
        creditScore.Score = baseScore;
        creditScore.Rating = rating;
        creditScore.MaximumCreditLimit = creditLimit;
        creditScore.InterestRate = interestRate;
        creditScore.TotalTransactions = totalTransactions;
        creditScore.TotalVolume = totalVolume;
        creditScore.AccountAgeDays = accountAgeDays;
        creditScore.HasKyc = kyc?.Status == KycStatus.Approved;
        creditScore.LastCalculatedAt = DateTime.UtcNow;

        // Replace factors
        var existingFactors = await _context.CreditScoreFactors
            .Where(csf => csf.CreditScoreId == creditScore.Id)
            .ToListAsync();
        _context.CreditScoreFactors.RemoveRange(existingFactors);
        creditScore.Factors = factors;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Credit score calculated for user {UserId}: {Score} ({Rating})", userId, baseScore, rating);
        return creditScore;
    }

    public async Task<List<CreditScoreFactor>> GetCreditScoreFactorsAsync(string userId)
    {
        var creditScore = await GetOrCreateCreditScoreAsync(userId);
        return await _context.CreditScoreFactors
            .Where(csf => csf.CreditScoreId == creditScore.Id)
            .OrderByDescending(csf => csf.Impact)
            .ToListAsync();
    }
}
