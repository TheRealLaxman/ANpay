using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class BranchService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BranchService> _logger;

    public BranchService(ApplicationDbContext context, ILogger<BranchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Branch>> GetAllAsync()
    {
        return await _context.Branches
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<Branch>> GetActiveAsync()
    {
        return await _context.Branches
            .Where(b => b.Status == BranchStatus.Active)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<Branch> GetByIdAsync(Guid id)
    {
        var branch = await _context.Branches.FindAsync(id);
        if (branch == null) throw new NotFoundException("Branch not found");
        return branch;
    }

    public async Task<Branch> CreateAsync(string name, string address, string city, string phone)
    {
        if (await _context.Branches.AnyAsync(b => b.Name == name))
            throw new ValidationException("Branch name already exists");

        var branch = new Branch
        {
            Name = name,
            Address = address,
            City = city,
            Phone = phone,
            Status = BranchStatus.Active
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Branch created: {Name}", name);
        return branch;
    }

    public async Task<Branch> UpdateAsync(Guid id, string name, string address, string city, string phone, BranchStatus status)
    {
        var branch = await _context.Branches.FindAsync(id)
            ?? throw new NotFoundException("Branch not found");

        if (await _context.Branches.AnyAsync(b => b.Name == name && b.Id != id))
            throw new ValidationException("Branch name already exists");

        branch.Name = name;
        branch.Address = address;
        branch.City = city;
        branch.Phone = phone;
        branch.Status = status;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Branch updated: {Id}", id);
        return branch;
    }

    public async Task DeleteAsync(Guid id)
    {
        var branch = await _context.Branches.FindAsync(id)
            ?? throw new NotFoundException("Branch not found");

        if (await _context.Users.AnyAsync(u => u.BranchId == id))
            throw new ValidationException("Cannot delete branch with assigned users");

        _context.Branches.Remove(branch);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Branch deleted: {Id}", id);
    }

    public async Task<BranchDashboardDto> GetDashboardAsync(Guid branchId)
    {
        var branch = await _context.Branches.FindAsync(branchId)
            ?? throw new NotFoundException("Branch not found");

        var employeeCount = await _context.Users.CountAsync(u => u.BranchId == branchId && u.IsActive);
        var today = DateTime.UtcNow.Date;

        var todayTransactions = await _context.Transactions
            .Where(t => t.Wallet.User.BranchId == branchId && t.CreatedAt >= today)
            .ToListAsync();

        var pendingApprovals = await _context.ApprovalRequests
            .CountAsync(ar => ar.Status == ApprovalStatus.Pending && ar.RequestedBy.BranchId == branchId);

        var cashBalance = await _context.CashBalances
            .Where(cb => cb.BranchId == branchId && cb.Date == today)
            .FirstOrDefaultAsync();

        return new BranchDashboardDto
        {
            BranchName = branch.Name,
            EmployeeCount = employeeCount,
            TodayDeposits = todayTransactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.Amount),
            TodayWithdrawals = todayTransactions.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.Amount),
            TodayTransfers = todayTransactions.Count(t => t.Type == TransactionType.TransferOut),
            PendingApprovals = pendingApprovals,
            CashBalance = cashBalance?.ExpectedClosing ?? 0,
            Status = branch.Status
        };
    }
}

public class BranchDashboardDto
{
    public string BranchName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal TodayDeposits { get; set; }
    public decimal TodayWithdrawals { get; set; }
    public int TodayTransfers { get; set; }
    public int PendingApprovals { get; set; }
    public decimal CashBalance { get; set; }
    public BranchStatus Status { get; set; }
}
