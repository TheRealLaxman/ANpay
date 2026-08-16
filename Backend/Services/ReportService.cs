using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class ReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ApplicationDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DashboardStats> GetSuperAdminStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var totalUsers = await _context.Users.CountAsync(u => u.IsActive);
        var totalWallets = await _context.Wallets.CountAsync();
        var totalBalance = await _context.Wallets.SumAsync(w => w.Balance);
        var totalBranches = await _context.Branches.CountAsync(b => b.Status == BranchStatus.Active);
        var todayTransactions = await _context.Transactions.CountAsync(t => t.CreatedAt >= today);
        var monthTransactions = await _context.Transactions.CountAsync(t => t.CreatedAt >= monthStart);
        var todayVolume = await _context.Transactions
            .Where(t => t.CreatedAt >= today)
            .SumAsync(t => t.Amount);
        var monthVolume = await _context.Transactions
            .Where(t => t.CreatedAt >= monthStart)
            .SumAsync(t => t.Amount);
        var pendingApprovals = await _context.ApprovalRequests.CountAsync(ar => ar.Status == ApprovalStatus.Pending);
        var pendingKyc = await _context.KycProfiles.CountAsync(kp => kp.Status == KycStatus.Submitted);
        var openTickets = await _context.SupportTickets.CountAsync(st => st.Status != TicketStatus.Closed && st.Status != TicketStatus.Resolved);

        return new DashboardStats
        {
            TotalUsers = totalUsers,
            TotalWallets = totalWallets,
            TotalBalance = totalBalance,
            TotalBranches = totalBranches,
            TodayTransactions = todayTransactions,
            MonthTransactions = monthTransactions,
            TodayVolume = todayVolume,
            MonthVolume = monthVolume,
            PendingApprovals = pendingApprovals,
            PendingKyc = pendingKyc,
            OpenTickets = openTickets
        };
    }

    public async Task<List<TransactionReportDto>> GetTransactionReportAsync(DateTime from, DateTime to, Guid? branchId = null)
    {
        var query = _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.User)
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to);

        if (branchId.HasValue)
            query = query.Where(t => t.Wallet.User.BranchId == branchId.Value);

        var transactions = await query.ToListAsync();

        return transactions.Select(t => new TransactionReportDto
        {
            TransactionId = t.Id,
            Type = t.Type.ToString(),
            Amount = t.Amount,
            Currency = t.Wallet.Currency,
            Status = t.Status.ToString(),
            UserEmail = t.Wallet.User.Email ?? "",
            UserName = $"{t.Wallet.User.FirstName} {t.Wallet.User.LastName}",
            BranchId = t.Wallet.User.BranchId,
            CreatedAt = t.CreatedAt
        }).ToList();
    }

    public async Task<List<BranchReportDto>> GetBranchReportAsync()
    {
        return await _context.Branches
            .Select(b => new BranchReportDto
            {
                BranchId = b.Id,
                BranchName = b.Name,
                Status = b.Status.ToString(),
                EmployeeCount = b.Employees.Count(e => e.IsActive),
                TodayTransactions = b.Employees
                    .SelectMany(e => e.Wallets)
                    .SelectMany(w => w.Transactions)
                    .Count(t => t.CreatedAt >= DateTime.UtcNow.Date),
                TodayVolume = b.Employees
                    .SelectMany(e => e.Wallets)
                    .SelectMany(w => w.Transactions)
                    .Where(t => t.CreatedAt >= DateTime.UtcNow.Date)
                    .Sum(t => t.Amount)
            })
            .ToListAsync();
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to)
    {
        var completedTransactions = await _context.Transactions
            .Include(t => t.Wallet)
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to && t.Status == TransactionStatus.Completed)
            .ToListAsync();

        var totalTransactions = completedTransactions.Count;

        var totalFeesCollected = completedTransactions
            .Where(t => t.Type == TransactionType.Withdrawal || t.Type == TransactionType.TransferOut || t.Type == TransactionType.Payment)
            .Sum(t => t.Fee);

        var exchangeVolume = completedTransactions
            .Where(t => t.Type == TransactionType.Deposit || t.Type == TransactionType.TransferIn)
            .Sum(t => t.Fee);

        var averageTransactionAmount = totalTransactions > 0
            ? completedTransactions.Average(t => t.Amount)
            : 0m;

        return new RevenueReportDto
        {
            From = from,
            To = to,
            TotalFeesCollected = totalFeesCollected,
            ExchangeRevenue = exchangeVolume,
            TotalTransactions = totalTransactions,
            AverageTransactionAmount = averageTransactionAmount,
            TotalVolume = completedTransactions.Sum(t => t.Amount)
        };
    }

    public async Task<List<BranchComparisonReportDto>> GetBranchComparisonReportAsync()
    {
        var branches = await _context.Branches
            .Include(b => b.Employees)
            .ThenInclude(e => e.Wallets)
            .ThenInclude(w => w.Transactions)
            .ToListAsync();

        var today = DateTime.UtcNow.Date;

        return branches.Select(b => new BranchComparisonReportDto
        {
            BranchId = b.Id,
            BranchName = b.Name,
            Status = b.Status.ToString(),
            EmployeeCount = b.Employees.Count(e => e.IsActive),
            TotalTransactions = b.Employees
                .SelectMany(e => e.Wallets)
                .SelectMany(w => w.Transactions)
                .Count(),
            TodayTransactions = b.Employees
                .SelectMany(e => e.Wallets)
                .SelectMany(w => w.Transactions)
                .Count(t => t.CreatedAt >= today),
            TotalVolume = b.Employees
                .SelectMany(e => e.Wallets)
                .SelectMany(w => w.Transactions)
                .Sum(t => t.Amount),
            TodayVolume = b.Employees
                .SelectMany(e => e.Wallets)
                .SelectMany(w => w.Transactions)
                .Where(t => t.CreatedAt >= today)
                .Sum(t => t.Amount),
            WalletCount = b.Employees
                .SelectMany(e => e.Wallets)
                .Count(),
            TotalBalance = b.Employees
                .SelectMany(e => e.Wallets)
                .Sum(w => w.Balance)
        }).ToList();
    }

    public async Task<List<TransactionReportDto>> GetCustomerStatementAsync(string userId, DateTime from, DateTime to)
    {
        var transactions = await _context.Transactions
            .Include(t => t.Wallet)
            .ThenInclude(w => w.User)
            .Where(t => t.Wallet.UserId == userId && t.CreatedAt >= from && t.CreatedAt <= to)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return transactions.Select(t => new TransactionReportDto
        {
            TransactionId = t.Id,
            Type = t.Type.ToString(),
            Amount = t.Amount,
            Currency = t.Wallet.Currency,
            Status = t.Status.ToString(),
            UserEmail = t.Wallet.User.Email ?? "",
            UserName = $"{t.Wallet.User.FirstName} {t.Wallet.User.LastName}",
            BranchId = t.Wallet.User.BranchId,
            CreatedAt = t.CreatedAt
        }).ToList();
    }
}

public class DashboardStats
{
    public int TotalUsers { get; set; }
    public int TotalWallets { get; set; }
    public decimal TotalBalance { get; set; }
    public int TotalBranches { get; set; }
    public int TodayTransactions { get; set; }
    public int MonthTransactions { get; set; }
    public decimal TodayVolume { get; set; }
    public decimal MonthVolume { get; set; }
    public int PendingApprovals { get; set; }
    public int PendingKyc { get; set; }
    public int OpenTickets { get; set; }
}

public class TransactionReportDto
{
    public Guid TransactionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BranchReportDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int TodayTransactions { get; set; }
    public decimal TodayVolume { get; set; }
}

public class RevenueReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalFeesCollected { get; set; }
    public decimal ExchangeRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionAmount { get; set; }
    public decimal TotalVolume { get; set; }
}

public class BranchComparisonReportDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int TotalTransactions { get; set; }
    public int TodayTransactions { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TodayVolume { get; set; }
    public int WalletCount { get; set; }
    public decimal TotalBalance { get; set; }
}
