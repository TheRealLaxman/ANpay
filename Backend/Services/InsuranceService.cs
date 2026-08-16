using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class InsuranceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InsuranceService> _logger;

    public InsuranceService(ApplicationDbContext context, ILogger<InsuranceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Insurance> PurchaseInsuranceAsync(string userId, Guid walletId, InsuranceType type, string planName, decimal premiumAmount, decimal coverageAmount, InsuranceFrequency frequency, int durationMonths = 12)
    {
        _logger.LogInformation("Insurance purchase by user {UserId}, type {Type}", userId, type);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        if (wallet.AvailableBalance < premiumAmount)
            throw new ValidationException("Insufficient available balance for premium payment");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            wallet.Balance -= premiumAmount;

            var insurance = new Insurance
            {
                UserId = userId,
                WalletId = walletId,
                Type = type,
                PlanName = planName,
                PremiumAmount = premiumAmount,
                CoverageAmount = coverageAmount,
                Currency = wallet.Currency,
                Frequency = frequency,
                Status = InsuranceStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(durationMonths),
                LastPaymentDate = DateTime.UtcNow,
                NextPaymentDate = CalculateNextPaymentDate(DateTime.UtcNow, frequency),
                PolicyNumber = $"INS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}"
            };

            _context.Insurances.Add(insurance);

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Payment,
                Amount = premiumAmount,
                BalanceBefore = wallet.Balance + premiumAmount,
                BalanceAfter = wallet.Balance,
                Description = $"Insurance premium - {planName}",
                ReferenceNumber = insurance.PolicyNumber,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return insurance;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Insurance>> GetUserInsurancesAsync(string userId)
    {
        return await _context.Insurances
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Insurance?> GetInsuranceByIdAsync(Guid id, string userId)
    {
        return await _context.Insurances.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
    }

    public async Task<InsuranceClaim> FileClaimAsync(Guid insuranceId, string userId, string title, string description, decimal amount, string? documents = null)
    {
        var insurance = await _context.Insurances.FirstOrDefaultAsync(i => i.Id == insuranceId && i.UserId == userId);
        if (insurance == null) throw new NotFoundException("Insurance not found");
        if (insurance.Status != InsuranceStatus.Active) throw new ValidationException("Insurance is not active");

        var claim = new InsuranceClaim
        {
            InsuranceId = insuranceId,
            ClaimTitle = title,
            ClaimDescription = description,
            ClaimAmount = amount,
            Status = ClaimStatus.Submitted,
            SupportingDocuments = documents
        };

        _context.InsuranceClaims.Add(claim);
        insurance.TotalClaims++;
        await _context.SaveChangesAsync();

        return claim;
    }

    public async Task<List<InsuranceClaim>> GetClaimsAsync(Guid insuranceId, string userId)
    {
        var insurance = await _context.Insurances.FirstOrDefaultAsync(i => i.Id == insuranceId && i.UserId == userId);
        if (insurance == null) throw new NotFoundException("Insurance not found");

        return await _context.InsuranceClaims
            .Where(ic => ic.InsuranceId == insuranceId)
            .OrderByDescending(ic => ic.SubmittedDate)
            .ToListAsync();
    }

    private DateTime CalculateNextPaymentDate(DateTime fromDate, InsuranceFrequency frequency)
    {
        return frequency switch
        {
            InsuranceFrequency.Weekly => fromDate.AddDays(7),
            InsuranceFrequency.Monthly => fromDate.AddMonths(1),
            InsuranceFrequency.Quarterly => fromDate.AddMonths(3),
            InsuranceFrequency.Yearly => fromDate.AddYears(1),
            _ => fromDate.AddMonths(1)
        };
    }
}
