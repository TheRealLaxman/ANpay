using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Workers;

public class InsuranceRenewalWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InsuranceRenewalWorker> _logger;

    public InsuranceRenewalWorker(IServiceProvider serviceProvider, ILogger<InsuranceRenewalWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InsuranceRenewalWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Process premium renewals
                await ProcessPremiumRenewalsAsync(context, stoppingToken);

                // Expire overdue policies
                await ExpireOverduePoliciesAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in insurance renewal processing");
            }

            // Run daily at 2 AM
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            var delay = nextRun - now;
            if (delay.TotalMilliseconds <= 0) delay = TimeSpan.Zero;
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("InsuranceRenewalWorker stopped");
    }

    private async Task ProcessPremiumRenewalsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var dueRenewals = await context.Insurances
            .Include(i => i.Wallet)
            .Where(i => i.Status == InsuranceStatus.Active
                        && i.NextPaymentDate <= DateTime.UtcNow
                        && i.Wallet != null)
            .ToListAsync(ct);

        foreach (var insurance in dueRenewals)
        {
            if (insurance.Wallet == null) continue;

            if (insurance.Wallet.Balance < insurance.PremiumAmount)
            {
                _logger.LogWarning("Insufficient balance for insurance renewal {InsuranceId}. Balance: {Balance}, needed: {Amount}",
                    insurance.Id, insurance.Wallet.Balance, insurance.PremiumAmount);
                continue;
            }

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);

                insurance.Wallet.Balance -= insurance.PremiumAmount;
                insurance.LastPaymentDate = DateTime.UtcNow;
                insurance.NextPaymentDate = CalculateNextPaymentDate(DateTime.UtcNow, insurance.Frequency);

                context.Transactions.Add(new Transaction
                {
                    WalletId = insurance.WalletId,
                    Type = TransactionType.Payment,
                    Amount = insurance.PremiumAmount,
                    BalanceBefore = insurance.Wallet.Balance + insurance.PremiumAmount,
                    BalanceAfter = insurance.Wallet.Balance,
                    Description = $"Insurance premium renewal - {insurance.PlanName}",
                    ReferenceNumber = $"INS-REN-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    Status = TransactionStatus.Completed
                });

                await context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Insurance renewal processed for {InsuranceId}. Next payment: {NextPayment}",
                    insurance.Id, insurance.NextPaymentDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Insurance renewal failed for {InsuranceId}", insurance.Id);
            }
        }
    }

    private async Task ExpireOverduePoliciesAsync(ApplicationDbContext context, CancellationToken ct)
    {
        // Apply 15-day grace period before expiring policies
        var gracePeriodDeadline = DateTime.UtcNow.AddDays(-15);
        var expiredPolicies = await context.Insurances
            .Where(i => i.Status == InsuranceStatus.Active && i.EndDate < gracePeriodDeadline)
            .ToListAsync(ct);

        foreach (var policy in expiredPolicies)
        {
            policy.Status = InsuranceStatus.Expired;
            _logger.LogInformation("Insurance policy {PolicyId} expired", policy.Id);
        }

        if (expiredPolicies.Any())
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static DateTime CalculateNextPaymentDate(DateTime fromDate, InsuranceFrequency frequency)
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
