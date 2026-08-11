using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class CashService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CashService> _logger;

    public CashService(ApplicationDbContext context, ILogger<CashService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CashBalance> GetTodayBalanceAsync(Guid branchId, string? employeeId = null)
    {
        var today = DateTime.UtcNow.Date;
        var balance = await _context.CashBalances
            .FirstOrDefaultAsync(cb => cb.BranchId == branchId && cb.Date == today && cb.EmployeeId == employeeId);

        if (balance == null)
        {
            balance = new CashBalance
            {
                BranchId = branchId,
                EmployeeId = employeeId,
                Date = today
            };
            _context.CashBalances.Add(balance);
            await _context.SaveChangesAsync();
        }

        return balance;
    }

    public async Task<CashBalance> AdjustCashAsync(Guid branchId, decimal adjustment, string? employeeId = null, string reason = "")
    {
        var balance = await GetTodayBalanceAsync(branchId, employeeId);
        balance.Adjustments += adjustment;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cash adjusted: Branch {BranchId}, Amount {Amount}, Reason {Reason}", branchId, adjustment, reason);
        return balance;
    }

    public async Task<CashBalance> RecordClosingAsync(Guid branchId, decimal actualClosing, string? employeeId = null)
    {
        var balance = await GetTodayBalanceAsync(branchId, employeeId);
        balance.ActualClosing = actualClosing;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cash closing recorded: Branch {BranchId}, Actual {Actual}, Expected {Expected}, Diff {Diff}",
            branchId, actualClosing, balance.ExpectedClosing, balance.Difference);
        return balance;
    }

    public async Task<List<CashBalance>> GetHistoryAsync(Guid branchId, DateTime from, DateTime to)
    {
        return await _context.CashBalances
            .Include(cb => cb.Branch)
            .Where(cb => cb.BranchId == branchId && cb.Date >= from && cb.Date <= to)
            .OrderByDescending(cb => cb.Date)
            .ToListAsync();
    }

    public async Task ReconcileAsync(Guid cashBalanceId)
    {
        var balance = await _context.CashBalances.FindAsync(cashBalanceId)
            ?? throw new NotFoundException("Cash balance not found");

        if (Math.Abs(balance.Difference) > 0.01m)
            throw new ValidationException($"Cash mismatch: difference is {balance.Difference}");

        balance.IsReconciled = true;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cash reconciled: {Id}", cashBalanceId);
    }
}
