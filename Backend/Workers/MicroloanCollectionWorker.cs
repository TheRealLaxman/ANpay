using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Workers;

public class MicroloanCollectionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MicroloanCollectionWorker> _logger;

    public MicroloanCollectionWorker(IServiceProvider serviceProvider, ILogger<MicroloanCollectionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MicroloanCollectionWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await ProcessAutoDebitAsync(context, stoppingToken);
                await ProcessOverdueLoansAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in microloan collection processing");
            }

            // Run every 6 hours
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }

        _logger.LogInformation("MicroloanCollectionWorker stopped");
    }

    private async Task ProcessAutoDebitAsync(ApplicationDbContext context, CancellationToken ct)
    {
        // Get loans with auto-debit enabled and pending repayments
        var loansWithAutoDebit = await context.Microloans
            .Include(ml => ml.Wallet)
            .Include(ml => ml.Repayments)
            .Where(ml => ml.AutoDebitEnabled
                        && (ml.Status == MicroloanStatus.Disbursed || ml.Status == MicroloanStatus.Repaying)
                        && ml.Wallet!.Balance > 0)
            .ToListAsync(ct);

        foreach (var loan in loansWithAutoDebit)
        {
            var pendingRepayment = loan.Repayments
                .Where(r => r.Status == MicroloanRepaymentStatus.Pending && r.DueDate <= DateTime.UtcNow)
                .OrderBy(r => r.DueDate)
                .FirstOrDefault();

            if (pendingRepayment == null) continue;
            if (loan.Wallet == null) continue;

            var amountToPay = pendingRepayment.Amount;
            if (loan.Wallet.Balance < amountToPay)
            {
                _logger.LogWarning("Insufficient balance for auto-debit on loan {LoanId}. Wallet balance: {Balance}, needed: {Amount}",
                    loan.Id, loan.Wallet.Balance, amountToPay);
                continue;
            }

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);

                loan.Wallet.Balance -= amountToPay;

                pendingRepayment.Status = MicroloanRepaymentStatus.Paid;
                pendingRepayment.PaidDate = DateTime.UtcNow;
                pendingRepayment.TransactionReference = $"LOAN-AUTO-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                loan.OutstandingAmount -= amountToPay;
                if (loan.OutstandingAmount < 0) loan.OutstandingAmount = 0;
                loan.Status = MicroloanStatus.Repaying;

                if (loan.OutstandingAmount <= 0)
                {
                    loan.Status = MicroloanStatus.Completed;
                    loan.CompletedDate = DateTime.UtcNow;
                }

                context.Transactions.Add(new Transaction
                {
                    WalletId = loan.WalletId,
                    Type = TransactionType.Withdrawal,
                    Amount = amountToPay,
                    BalanceBefore = loan.Wallet.Balance + amountToPay,
                    BalanceAfter = loan.Wallet.Balance,
                    Description = $"Auto-debit loan repayment ({loan.Id})",
                    ReferenceNumber = pendingRepayment.TransactionReference,
                    Status = TransactionStatus.Completed
                });

                await context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Auto-debit successful for loan {LoanId}. Amount: {Amount}", loan.Id, amountToPay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-debit failed for loan {LoanId}", loan.Id);
            }
        }
    }

    private async Task ProcessOverdueLoansAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var overdueLoans = await context.Microloans
            .Where(ml => (ml.Status == MicroloanStatus.Disbursed || ml.Status == MicroloanStatus.Repaying)
                        && ml.DueDate.HasValue && ml.DueDate.Value < DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var loan in overdueLoans)
        {
            var daysOverdue = (DateTime.UtcNow - loan.DueDate!.Value).Days;

            if (daysOverdue > 30 && loan.Status != MicroloanStatus.Defaulted)
            {
                // Mark as defaulted after 30 days overdue
                loan.Status = MicroloanStatus.Defaulted;
                loan.DaysOverdue = daysOverdue;
                loan.PenaltyAmount = loan.OutstandingAmount * 0.1m; // 10% penalty

                _logger.LogWarning("Loan {LoanId} defaulted. Days overdue: {Days}", loan.Id, daysOverdue);
            }
            else
            {
                loan.DaysOverdue = daysOverdue;
                // Apply late fee penalty
                if (daysOverdue > 0 && loan.PenaltyAmount == 0)
                {
                    loan.PenaltyAmount = loan.OutstandingAmount * 0.02m; // 2% late fee
                }
            }
        }

        if (overdueLoans.Any())
        {
            await context.SaveChangesAsync(ct);
        }
    }
}
