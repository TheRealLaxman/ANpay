using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Workers;

public class InvestmentAccrualWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvestmentAccrualWorker> _logger;

    public InvestmentAccrualWorker(IServiceProvider serviceProvider, ILogger<InvestmentAccrualWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InvestmentAccrualWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Process active investments - accrue interest daily
                await AccrueInterestAsync(context, stoppingToken);

                // Process matured investments
                await ProcessMaturityAsync(context, stoppingToken);

                // Process auto-save savings goals
                await ProcessAutoSaveAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in investment accrual processing");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("InvestmentAccrualWorker stopped");
    }

    private async Task AccrueInterestAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var activeInvestments = await context.Investments
            .Where(i => i.Status == InvestmentStatus.Active && i.MaturityDate > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var investment in activeInvestments)
        {
            // Calculate daily interest accrual
            var dailyRate = investment.InterestRate / 100 / 365;
            var dailyInterest = investment.PrincipalAmount * dailyRate;

            if (dailyInterest > 0)
            {
                investment.InterestEarned += dailyInterest;
                investment.CurrentValue = investment.PrincipalAmount + investment.InterestEarned;

                // Record the accrual transaction (only once per day)
                var today = DateTime.UtcNow.Date;
                var existingAccrual = await context.InvestmentTransactions
                    .FirstOrDefaultAsync(it => it.InvestmentId == investment.Id
                                            && it.Type == InvestmentTransactionType.Interest
                                            && it.CreatedAt.Date == today, ct);

                if (existingAccrual == null)
                {
                    context.InvestmentTransactions.Add(new InvestmentTransaction
                    {
                        InvestmentId = investment.Id,
                        Type = InvestmentTransactionType.Interest,
                        Amount = dailyInterest,
                        Description = $"Daily interest accrual ({investment.InterestRate}% p.a.)",
                        ReferenceNumber = $"INT-{DateTime.UtcNow:yyyyMMdd}-{investment.Id.ToString()[..8].ToUpper()}"
                    });
                }
            }
        }

        if (activeInvestments.Any())
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Interest accrued for {Count} investments", activeInvestments.Count);
        }
    }

    private async Task ProcessMaturityAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var maturedInvestments = await context.Investments
            .Where(i => i.Status == InvestmentStatus.Active && i.MaturityDate <= DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var investment in maturedInvestments)
        {
            investment.Status = InvestmentStatus.Matured;

            if (investment.AutoRenew)
            {
                // Auto-renew: extend maturity date
                investment.MaturityDate = DateTime.UtcNow.AddDays(investment.TenureDays);
                investment.Status = InvestmentStatus.Active;
                investment.IsLocked = false;

                _logger.LogInformation("Investment {Id} auto-renewed until {Maturity}", investment.Id, investment.MaturityDate);
            }
            else
            {
                // Credit wallet with principal + interest
                var wallet = await context.Wallets.FindAsync(investment.WalletId);
                if (wallet != null)
                {
                    var payoutAmount = investment.CurrentValue + investment.InterestEarned;
                    wallet.Balance += payoutAmount;

                    context.Transactions.Add(new Transaction
                    {
                        WalletId = investment.WalletId,
                        Type = TransactionType.Deposit,
                        Amount = payoutAmount,
                        BalanceBefore = wallet.Balance - payoutAmount,
                        BalanceAfter = wallet.Balance,
                        Description = $"Investment maturity payout - {investment.ProductName}",
                        ReferenceNumber = $"INV-MAT-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                        Status = TransactionStatus.Completed
                    });

                    context.InvestmentTransactions.Add(new InvestmentTransaction
                    {
                        InvestmentId = investment.Id,
                        Type = InvestmentTransactionType.Withdrawal,
                        Amount = payoutAmount,
                        Description = "Maturity payout",
                        ReferenceNumber = $"INV-MAT-{Guid.NewGuid().ToString()[..8].ToUpper()}"
                    });

                    _logger.LogInformation("Investment {Id} matured. Payout: {Amount}", investment.Id, payoutAmount);
                }
            }
        }

        if (maturedInvestments.Any())
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task ProcessAutoSaveAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var autoSaveGoals = await context.SavingsGoals
            .Where(sg => sg.Status == SavingsGoalStatus.Active
                        && sg.AutoSave
                        && sg.AutoSaveAmount > 0
                        && sg.CurrentAmount < sg.TargetAmount)
            .ToListAsync(ct);

        foreach (var goal in autoSaveGoals)
        {
            var shouldSave = goal.AutoSaveFrequency switch
            {
                SavingsGoalFrequency.Daily => true,
                SavingsGoalFrequency.Weekly => (DateTime.UtcNow - goal.CreatedAt).Days % 7 == 0,
                SavingsGoalFrequency.Biweekly => (DateTime.UtcNow - goal.CreatedAt).Days % 14 == 0,
                SavingsGoalFrequency.Monthly => DateTime.UtcNow.Day == goal.CreatedAt.Day,
                _ => false
            };

            if (!shouldSave) continue;

            var wallet = await context.Wallets.FindAsync(goal.WalletId);
            if (wallet == null || wallet.Balance < goal.AutoSaveAmount) continue;

            var remainingNeeded = goal.TargetAmount - goal.CurrentAmount;
            var amountToSave = Math.Min(goal.AutoSaveAmount, remainingNeeded);

            wallet.Balance -= amountToSave;
            goal.CurrentAmount += amountToSave;

            if (goal.CurrentAmount >= goal.TargetAmount)
            {
                goal.Status = SavingsGoalStatus.Completed;
                goal.CompletedAt = DateTime.UtcNow;
            }

            context.Transactions.Add(new Transaction
            {
                WalletId = goal.WalletId,
                Type = TransactionType.TransferOut,
                Amount = amountToSave,
                BalanceBefore = wallet.Balance + amountToSave,
                BalanceAfter = wallet.Balance,
                Description = $"Auto-save to goal: {goal.GoalName}",
                ReferenceNumber = $"SG-AUTO-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            });

            _logger.LogInformation("Auto-saved {Amount} to goal {GoalName}", amountToSave, goal.GoalName);
        }

        await context.SaveChangesAsync(ct);
    }
}
