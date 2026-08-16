using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class MicroloanService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MicroloanService> _logger;
    private readonly CreditScoreService _creditScoreService;

    public MicroloanService(ApplicationDbContext context, ILogger<MicroloanService> logger, CreditScoreService creditScoreService)
    {
        _context = context;
        _logger = logger;
        _creditScoreService = creditScoreService;
    }

    public async Task<Microloan> ApplyForLoanAsync(string userId, Guid walletId, decimal amount, int tenureDays, MicroloanPurpose purpose, string? purposeDescription = null)
    {
        _logger.LogInformation("Loan application from user {UserId} for {Amount}", userId, amount);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        if (amount < 5000 || amount > 500000)
            throw new ValidationException("Loan amount must be between ₦5,000 and ₦500,000");

        if (tenureDays < 7 || tenureDays > 90)
            throw new ValidationException("Tenure must be between 7 and 90 days");

        // Check existing active loans
        var activeLoans = await _context.Microloans
            .CountAsync(ml => ml.UserId == userId && (ml.Status == MicroloanStatus.Disbursed || ml.Status == MicroloanStatus.Repaying));
        if (activeLoans >= 3)
            throw new ValidationException("You can have at most 3 active loans");

        // Calculate credit score
        var creditScore = await _creditScoreService.CalculateCreditScoreAsync(userId);

        // Interest rate based on credit score
        var interestRate = creditScore.Rating switch
        {
            CreditRating.Exceptional => 5,
            CreditRating.VeryGood => 8,
            CreditRating.Good => 12,
            CreditRating.Fair => 18,
            CreditRating.Poor => 25,
            _ => 30
        };

        var interestAmount = amount * interestRate / 100;
        var totalRepayable = amount + interestAmount;

        var loan = new Microloan
        {
            UserId = userId,
            WalletId = walletId,
            PrincipalAmount = amount,
            DisbursedAmount = amount,
            OutstandingAmount = totalRepayable,
            InterestAmount = interestAmount,
            TotalRepayable = totalRepayable,
            InterestRate = interestRate,
            TenureDays = tenureDays,
            RepaymentFrequencyDays = Math.Max(7, tenureDays / 4),
            Status = MicroloanStatus.Approved,
            Purpose = purpose,
            PurposeDescription = purposeDescription,
            CreditScoreAtApplication = creditScore.Score,
            DueDate = DateTime.UtcNow.AddDays(tenureDays)
        };

        _context.Microloans.Add(loan);

        // Create repayment schedule
        var numRepayments = tenureDays / loan.RepaymentFrequencyDays;
        var installmentAmount = totalRepayable / numRepayments;

        for (int i = 1; i <= numRepayments; i++)
        {
            loan.Repayments.Add(new MicroloanRepayment
            {
                Amount = installmentAmount,
                DueDate = DateTime.UtcNow.AddDays(i * loan.RepaymentFrequencyDays),
                Status = MicroloanRepaymentStatus.Pending
            });
        }

        await _context.SaveChangesAsync();
        return loan;
    }

    public async Task<Microloan> ApproveLoanAsync(Guid loanId)
    {
        var loan = await _context.Microloans.FindAsync(loanId);
        if (loan == null) throw new NotFoundException("Loan not found");
        if (loan.Status != MicroloanStatus.Approved) throw new ValidationException("Loan is not in approved status");

        loan.Status = MicroloanStatus.Disbursed;
        loan.DisbursedDate = DateTime.UtcNow;

        // Disburse to wallet
        var wallet = await _context.Wallets.FindAsync(loan.WalletId);
        if (wallet != null)
        {
            wallet.Balance += loan.DisbursedAmount;

            var txRecord = new Transaction
            {
                WalletId = loan.WalletId,
                Type = TransactionType.Deposit,
                Amount = loan.DisbursedAmount,
                BalanceBefore = wallet.Balance - loan.DisbursedAmount,
                BalanceAfter = wallet.Balance,
                Description = $"Microloan disbursement (ID: {loan.Id})",
                ReferenceNumber = $"LOAN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[8..].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
        }

        await _context.SaveChangesAsync();
        return loan;
    }

    public async Task<MicroloanRepayment> MakeRepaymentAsync(Guid loanId, Guid walletId, string userId)
    {
        var loan = await _context.Microloans.FirstOrDefaultAsync(ml => ml.Id == loanId && ml.UserId == userId);
        if (loan == null) throw new NotFoundException("Loan not found");

        if (loan.Status != MicroloanStatus.Disbursed && loan.Status != MicroloanStatus.Repaying)
            throw new ValidationException("Loan is not active");

        var pendingRepayment = loan.Repayments
            .Where(r => r.Status == MicroloanRepaymentStatus.Pending)
            .OrderBy(r => r.DueDate)
            .FirstOrDefault();

        if (pendingRepayment == null) throw new ValidationException("No pending repayments");

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        var amountToPay = pendingRepayment.Amount;
        if (wallet.Balance < amountToPay)
            throw new ValidationException("Insufficient balance");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            wallet.Balance -= amountToPay;

            pendingRepayment.Status = MicroloanRepaymentStatus.Paid;
            pendingRepayment.PaidDate = DateTime.UtcNow;
            pendingRepayment.TransactionReference = $"LOAN-PAY-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            loan.OutstandingAmount -= amountToPay;
            loan.Status = MicroloanStatus.Repaying;

            if (loan.OutstandingAmount <= 0)
            {
                loan.Status = MicroloanStatus.Completed;
                loan.CompletedDate = DateTime.UtcNow;
            }

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Withdrawal,
                Amount = amountToPay,
                BalanceBefore = wallet.Balance + amountToPay,
                BalanceAfter = wallet.Balance,
                Description = $"Loan repayment ({loan.Id})",
                ReferenceNumber = pendingRepayment.TransactionReference,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return pendingRepayment;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Microloan>> GetUserLoansAsync(string userId)
    {
        return await _context.Microloans
            .Where(ml => ml.UserId == userId)
            .OrderByDescending(ml => ml.AppliedDate)
            .ToListAsync();
    }

    public async Task<Microloan?> GetLoanByIdAsync(Guid id, string userId)
    {
        return await _context.Microloans
            .FirstOrDefaultAsync(ml => ml.Id == id && ml.UserId == userId);
    }
}
