using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class ReconciliationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(ApplicationDbContext context, ILogger<ReconciliationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ReconciliationRecord> RunReconciliationAsync(ReconciliationType type, DateTime from, DateTime to, string source, decimal externalBalance)
    {
        _logger.LogInformation("Running {Type} reconciliation for {From} to {To}", type, from, to);

        var systemBalance = await CalculateSystemBalanceAsync(type, from, to);

        var record = new ReconciliationRecord
        {
            Source = source,
            Type = type,
            PeriodStart = from,
            PeriodEnd = to,
            SystemBalance = systemBalance,
            ExternalBalance = externalBalance,
            IsMatched = Math.Abs(systemBalance - externalBalance) < 0.01m,
            Status = Math.Abs(systemBalance - externalBalance) < 0.01m
                ? ReconciliationStatus.Matched
                : ReconciliationStatus.DiscrepancyFound,
            DiscrepancyDetails = Math.Abs(systemBalance - externalBalance) >= 0.01m
                ? $"System: {systemBalance}, External: {externalBalance}, Diff: {systemBalance - externalBalance}"
                : null
        };

        _context.ReconciliationRecords.Add(record);
        await _context.SaveChangesAsync();

        if (!record.IsMatched)
        {
            _logger.LogWarning("Reconciliation discrepancy detected: {Diff}", record.Difference);
            await FindMismatchedTransactionsAsync(record, type, from, to);
        }

        return record;
    }

    private async Task<decimal> CalculateSystemBalanceAsync(ReconciliationType type, DateTime from, DateTime to)
    {
        return type switch
        {
            ReconciliationType.WalletToBank => await _context.Transactions
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to
                    && t.Status == TransactionStatus.Completed
                    && (t.Type == TransactionType.Deposit || t.Type == TransactionType.Withdrawal))
                .SumAsync(t => t.Type == TransactionType.Deposit ? t.Amount : -t.Amount),

            ReconciliationType.WalletToPaymentGateway => await _context.Transactions
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to
                    && t.Status == TransactionStatus.Completed
                    && t.Channel == "API")
                .SumAsync(t => t.Amount),

            ReconciliationType.BranchCash => await _context.CashBalances
                .Where(cb => cb.Date >= from.Date && cb.Date <= to.Date)
                .SumAsync(cb => cb.TotalDeposits - cb.TotalWithdrawals),

            ReconciliationType.MerchantSettlement => await _context.MerchantSettlements
                .Where(ms => ms.CreatedAt >= from && ms.CreatedAt <= to)
                .SumAsync(ms => ms.GrossAmount),

            _ => 0
        };
    }

    private async Task FindMismatchedTransactionsAsync(ReconciliationRecord record, ReconciliationType type, DateTime from, DateTime to)
    {
        var transactions = type switch
        {
            ReconciliationType.WalletToBank => await _context.Transactions
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to
                    && t.Status == TransactionStatus.Completed
                    && (t.Type == TransactionType.Deposit || t.Type == TransactionType.Withdrawal))
                .Select(t => new ReconciliationTransaction
                {
                    TransactionId = t.Id,
                    Amount = t.Type == TransactionType.Deposit ? t.Amount : -t.Amount,
                    Description = $"{t.Type} - {t.ReferenceNumber}",
                    IsMatched = false,
                    MismatchReason = "Pending external verification"
                })
                .ToListAsync(),

            _ => new List<ReconciliationTransaction>()
        };

        foreach (var tx in transactions)
        {
            tx.ReconciliationRecordId = record.Id;
            _context.ReconciliationTransactions.Add(tx);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<ReconciliationRecord>> GetRecordsAsync(ReconciliationStatus? status = null)
    {
        var query = _context.ReconciliationRecords.AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<ReconciliationRecord?> GetRecordByIdAsync(Guid id)
    {
        return await _context.ReconciliationRecords
            .Include(r => r.ReconciliationTransactions)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task ResolveRecordAsync(Guid recordId, string? notes, string reviewedById)
    {
        var record = await _context.ReconciliationRecords.FindAsync(recordId)
            ?? throw new NotFoundException("Reconciliation record not found");

        record.Status = ReconciliationStatus.Resolved;
        record.Notes = notes;
        record.ReviewedById = reviewedById;
        record.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task EscalateRecordAsync(Guid recordId, string? notes)
    {
        var record = await _context.ReconciliationRecords.FindAsync(recordId)
            ?? throw new NotFoundException("Reconciliation record not found");

        record.Status = ReconciliationStatus.Escalated;
        record.Notes = notes;

        await _context.SaveChangesAsync();
    }
}
