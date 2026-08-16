using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class BnplService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BnplService> _logger;

    public BnplService(ApplicationDbContext context, ILogger<BnplService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BuyNowPayLater> CreateBnplAsync(string userId, Guid walletId, decimal totalAmount, int installments, BnplFrequency frequency, Guid? merchantId = null, Guid? merchantPaymentId = null, decimal downPayment = 0, decimal interestRate = 0)
    {
        _logger.LogInformation("Creating BNPL for user {UserId}, amount {Amount}", userId, totalAmount);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        if (totalAmount <= 0) throw new ValidationException("Total amount must be greater than zero");
        if (installments < 2 || installments > 12) throw new ValidationException("Installments must be between 2 and 12");

        var remainingAmount = totalAmount - downPayment;
        var totalWithInterest = remainingAmount * (1 + interestRate / 100);
        var installmentAmount = totalWithInterest / installments;

        var bnpl = new BuyNowPayLater
        {
            UserId = userId,
            WalletId = walletId,
            MerchantId = merchantId,
            MerchantPaymentId = merchantPaymentId,
            TotalAmount = totalAmount,
            DownPayment = downPayment,
            RemainingAmount = remainingAmount,
            TotalInstallments = installments,
            PaidInstallments = 0,
            InstallmentAmount = installmentAmount,
            Frequency = frequency,
            InterestRate = interestRate,
            Status = BnplStatus.Active,
            StartDate = DateTime.UtcNow,
            NextPaymentDate = CalculateNextPaymentDate(DateTime.UtcNow, frequency)
        };

        // Create installment schedule
        for (int i = 1; i <= installments; i++)
        {
            bnpl.Installments.Add(new BnplInstallment
            {
                InstallmentNumber = i,
                Amount = installmentAmount,
                DueDate = CalculateNextPaymentDate(DateTime.UtcNow.AddDays((i - 1) * GetFrequencyDays(frequency)), frequency),
                Status = BnplInstallmentStatus.Pending
            });
        }

        _context.BuyNowPayLaters.Add(bnpl);

        // Deduct down payment if applicable
        if (downPayment > 0)
        {
            if (wallet.Balance < downPayment)
                throw new ValidationException("Insufficient balance for down payment");

            wallet.Balance -= downPayment;

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Payment,
                Amount = downPayment,
                BalanceBefore = wallet.Balance + downPayment,
                BalanceAfter = wallet.Balance,
                Description = $"BNPL down payment",
                ReferenceNumber = $"BNPL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
        }

        await _context.SaveChangesAsync();
        return bnpl;
    }

    public async Task<List<BuyNowPayLater>> GetUserBnplsAsync(string userId)
    {
        return await _context.BuyNowPayLaters
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<BuyNowPayLater?> GetBnplByIdAsync(Guid id, string userId)
    {
        return await _context.BuyNowPayLaters
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
    }

    public async Task<BnplInstallment> PayInstallmentAsync(Guid bnplId, Guid walletId, string userId)
    {
        var bnpl = await _context.BuyNowPayLaters.FirstOrDefaultAsync(b => b.Id == bnplId && b.UserId == userId);
        if (bnpl == null) throw new NotFoundException("BNPL not found");

        if (bnpl.Status != BnplStatus.Active)
            throw new ValidationException("BNPL is not active");

        var nextInstallment = bnpl.Installments
            .Where(i => i.Status == BnplInstallmentStatus.Pending)
            .OrderBy(i => i.DueDate)
            .FirstOrDefault();

        if (nextInstallment == null)
            throw new ValidationException("No pending installments");

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        var amountToPay = nextInstallment.Amount + nextInstallment.PenaltyAmount;
        if (wallet.Balance < amountToPay)
            throw new ValidationException("Insufficient balance");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            wallet.Balance -= amountToPay;

            nextInstallment.Status = BnplInstallmentStatus.Paid;
            nextInstallment.PaidDate = DateTime.UtcNow;
            nextInstallment.TransactionReference = $"BNPL-PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            bnpl.PaidInstallments++;
            bnpl.RemainingAmount -= nextInstallment.Amount;

            if (bnpl.PaidInstallments >= bnpl.TotalInstallments)
            {
                bnpl.Status = BnplStatus.Completed;
                bnpl.EndDate = DateTime.UtcNow;
            }
            else
            {
                bnpl.NextPaymentDate = CalculateNextPaymentDate(bnpl.NextPaymentDate ?? DateTime.UtcNow, bnpl.Frequency);
            }

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Payment,
                Amount = amountToPay,
                BalanceBefore = wallet.Balance + amountToPay,
                BalanceAfter = wallet.Balance,
                Description = $"BNPL installment {nextInstallment.InstallmentNumber}/{bnpl.TotalInstallments}",
                ReferenceNumber = nextInstallment.TransactionReference,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return nextInstallment;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<BuyNowPayLater> PauseBnplAsync(Guid bnplId, string userId)
    {
        var bnpl = await _context.BuyNowPayLaters.FirstOrDefaultAsync(b => b.Id == bnplId && b.UserId == userId);
        if (bnpl == null) throw new NotFoundException("BNPL not found");
        bnpl.Status = BnplStatus.Cancelled;
        await _context.SaveChangesAsync();
        return bnpl;
    }

    private DateTime CalculateNextPaymentDate(DateTime fromDate, BnplFrequency frequency)
    {
        return frequency switch
        {
            BnplFrequency.Weekly => fromDate.AddDays(7),
            BnplFrequency.Biweekly => fromDate.AddDays(14),
            BnplFrequency.Monthly => fromDate.AddMonths(1),
            _ => fromDate.AddDays(7)
        };
    }

    private int GetFrequencyDays(BnplFrequency frequency)
    {
        return frequency switch
        {
            BnplFrequency.Weekly => 7,
            BnplFrequency.Biweekly => 14,
            BnplFrequency.Monthly => 30,
            _ => 7
        };
    }
}
