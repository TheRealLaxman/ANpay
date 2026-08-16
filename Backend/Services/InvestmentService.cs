using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class InvestmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InvestmentService> _logger;
    private readonly LedgerService _ledgerService;

    public InvestmentService(ApplicationDbContext context, ILogger<InvestmentService> logger, LedgerService ledgerService)
    {
        _context = context;
        _logger = logger;
        _ledgerService = ledgerService;
    }

    public async Task<Investment> CreateInvestmentAsync(string userId, Guid walletId, InvestmentType type, string productName, decimal amount, int tenureDays, decimal interestRate, bool autoRenew = false)
    {
        _logger.LogInformation("Investment creation by user {UserId}, type {Type}, amount {Amount}", userId, type, amount);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        if (amount < 10000) throw new ValidationException("Minimum investment is ₦10,000");
        if (wallet.AvailableBalance < amount) throw new ValidationException("Insufficient available balance");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            wallet.Balance -= amount;

            var investment = new Investment
            {
                UserId = userId,
                WalletId = walletId,
                Type = type,
                ProductName = productName,
                PrincipalAmount = amount,
                CurrentValue = amount,
                InterestEarned = 0,
                InterestRate = interestRate,
                TenureDays = tenureDays,
                Status = InvestmentStatus.Active,
                StartDate = DateTime.UtcNow,
                MaturityDate = DateTime.UtcNow.AddDays(tenureDays),
                AutoRenew = autoRenew,
                EarlyWithdrawalPenalty = amount * 0.02m
            };

            _context.Investments.Add(investment);

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Payment,
                Amount = amount,
                BalanceBefore = wallet.Balance + amount,
                BalanceAfter = wallet.Balance,
                Description = $"Investment - {productName}",
                ReferenceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);

            await _ledgerService.PostWalletWithdrawalAsync(txRecord.Id, walletId, amount, wallet.Currency);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return investment;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Investment>> GetUserInvestmentsAsync(string userId)
    {
        return await _context.Investments
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<Investment?> GetInvestmentByIdAsync(Guid id, string userId)
    {
        return await _context.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
    }

    public async Task<Investment> WithdrawInvestmentAsync(Guid investmentId, Guid walletId, string userId)
    {
        var investment = await _context.Investments.FirstOrDefaultAsync(i => i.Id == investmentId && i.UserId == userId);
        if (investment == null) throw new NotFoundException("Investment not found");
        if (investment.Status != InvestmentStatus.Active) throw new ValidationException("Investment is not active");

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        var isEarlyWithdrawal = DateTime.UtcNow < investment.MaturityDate;
        var penalty = isEarlyWithdrawal ? investment.EarlyWithdrawalPenalty : 0;
        var payoutAmount = investment.CurrentValue + investment.InterestEarned - penalty;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.Balance;
            wallet.Balance += payoutAmount;

            investment.Status = InvestmentStatus.Withdrawn;

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Deposit,
                Amount = payoutAmount,
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.Balance,
                Description = $"Investment withdrawal - {investment.ProductName}{(isEarlyWithdrawal ? " (early)" : "")}",
                ReferenceNumber = $"INV-WD-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);

            await _ledgerService.PostWalletDepositAsync(txRecord.Id, walletId, payoutAmount, wallet.Currency);

            var invTx = new InvestmentTransaction
            {
                InvestmentId = investmentId,
                Type = InvestmentTransactionType.Withdrawal,
                Amount = payoutAmount,
                Description = isEarlyWithdrawal ? "Early withdrawal" : "Maturity withdrawal",
                ReferenceNumber = txRecord.ReferenceNumber
            };

            _context.InvestmentTransactions.Add(invTx);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return investment;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SavingsGoal> CreateSavingsGoalAsync(string userId, Guid walletId, string name, decimal targetAmount, DateTime targetDate, string? description = null, bool autoSave = false, decimal autoSaveAmount = 0, SavingsGoalFrequency autoSaveFrequency = SavingsGoalFrequency.Weekly)
    {
        var savingsGoal = new SavingsGoal
        {
            UserId = userId,
            WalletId = walletId,
            GoalName = name,
            GoalDescription = description,
            TargetAmount = targetAmount,
            CurrentAmount = 0,
            TargetDate = targetDate,
            Status = SavingsGoalStatus.Active,
            AutoSave = autoSave,
            AutoSaveAmount = autoSaveAmount,
            AutoSaveFrequency = autoSaveFrequency
        };

        _context.SavingsGoals.Add(savingsGoal);
        await _context.SaveChangesAsync();
        return savingsGoal;
    }

    public async Task<List<SavingsGoal>> GetUserSavingsGoalsAsync(string userId)
    {
        return await _context.SavingsGoals
            .Where(sg => sg.UserId == userId)
            .OrderByDescending(sg => sg.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavingsGoal> ContributeToGoalAsync(Guid goalId, Guid walletId, string userId, decimal amount)
    {
        var goal = await _context.SavingsGoals.FirstOrDefaultAsync(sg => sg.Id == goalId && sg.UserId == userId);
        if (goal == null) throw new NotFoundException("Savings goal not found");

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        if (wallet.AvailableBalance < amount) throw new ValidationException("Insufficient available balance");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.Balance;
            wallet.Balance -= amount;
            goal.CurrentAmount += amount;

            if (goal.CurrentAmount >= goal.TargetAmount)
            {
                goal.Status = SavingsGoalStatus.Completed;
                goal.CompletedAt = DateTime.UtcNow;
            }

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.TransferOut,
                Amount = amount,
                BalanceBefore = wallet.Balance + amount,
                BalanceAfter = wallet.Balance,
                Description = $"Savings goal contribution - {goal.GoalName}",
                ReferenceNumber = $"SG-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return goal;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
