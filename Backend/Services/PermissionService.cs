using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class PermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(string userId, Permission permission)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return false;

        var role = ((AppUserRole)((int)user.Role)).ToString();

        return await _context.RolePermissions
            .AnyAsync(rp => rp.RoleName == role && rp.Permission == permission);
    }

    public async Task<List<Permission>> GetUserPermissionsAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return new List<Permission>();

        var role = ((AppUserRole)((int)user.Role)).ToString();

        return await _context.RolePermissions
            .Where(rp => rp.RoleName == role)
            .Select(rp => rp.Permission)
            .ToListAsync();
    }

    public async Task SeedPermissionsAsync()
    {
        if (await _context.RolePermissions.AnyAsync()) return;

        var allPermissions = Enum.GetValues<Permission>();

        var rolePermissions = new List<RolePermission>();

        // Customer - limited permissions
        var customerPerms = new[] { Permission.ViewWallets, Permission.ViewTransactions, Permission.CreateTransfer, Permission.SubmitKyc, Permission.ViewNotifications };
        foreach (var p in customerPerms)
            rolePermissions.Add(new RolePermission { RoleName = "Customer", Permission = p });

        // Official - employee level
        var officialPerms = new[] {
            Permission.ViewUsers, Permission.ViewWallets, Permission.ViewTransactions,
            Permission.CreateDeposit, Permission.CreateWithdrawal, Permission.CreateTransfer,
            Permission.ViewKyc, Permission.ViewReports, Permission.ViewTickets, Permission.RespondTickets,
            Permission.ViewCashBalance, Permission.ViewNotifications
        };
        foreach (var p in officialPerms)
            rolePermissions.Add(new RolePermission { RoleName = "Official", Permission = p });

        // BranchAdmin
        var branchAdminPerms = new[] {
            Permission.ViewUsers, Permission.CreateUsers, Permission.EditUsers, Permission.SuspendUsers,
            Permission.ViewBranches, Permission.ViewEmployees, Permission.CreateEmployees, Permission.EditEmployees,
            Permission.ViewWallets, Permission.FreezeWallets, Permission.AdjustBalance,
            Permission.ViewTransactions, Permission.CreateDeposit, Permission.CreateWithdrawal, Permission.CreateTransfer, Permission.ApproveTransactions, Permission.ReverseTransactions,
            Permission.ViewKyc, Permission.ReviewKyc, Permission.ApproveKyc, Permission.RejectKyc,
            Permission.ViewReports, Permission.ExportReports, Permission.ManageFees, Permission.ManageLimits,
            Permission.ViewAuditLogs, Permission.ViewTickets, Permission.RespondTickets, Permission.CloseTickets,
            Permission.ViewCashBalance, Permission.AdjustCash, Permission.PerformReconciliation,
            Permission.SendNotifications, Permission.ViewNotifications
        };
        foreach (var p in branchAdminPerms)
            rolePermissions.Add(new RolePermission { RoleName = "BranchAdmin", Permission = p });

        // MainBranchAdmin
        var mainBranchAdminPerms = new[] {
            Permission.ViewUsers, Permission.CreateUsers, Permission.EditUsers, Permission.SuspendUsers, Permission.DeleteUsers,
            Permission.ViewBranches, Permission.EditBranches,
            Permission.ViewEmployees, Permission.CreateEmployees, Permission.EditEmployees, Permission.DeleteEmployees,
            Permission.ViewWallets, Permission.CreateWallets, Permission.FreezeWallets, Permission.AdjustBalance,
            Permission.ViewTransactions, Permission.CreateDeposit, Permission.CreateWithdrawal, Permission.CreateTransfer, Permission.ApproveTransactions, Permission.ReverseTransactions,
            Permission.ViewKyc, Permission.ReviewKyc, Permission.ApproveKyc, Permission.RejectKyc,
            Permission.ViewReports, Permission.ExportReports, Permission.ManageFees, Permission.ManageLimits, Permission.ManageExchangeRates,
            Permission.ViewAuditLogs, Permission.ViewTickets, Permission.RespondTickets, Permission.CloseTickets,
            Permission.ViewCashBalance, Permission.AdjustCash, Permission.PerformReconciliation,
            Permission.SendNotifications, Permission.ViewNotifications
        };
        foreach (var p in mainBranchAdminPerms)
            rolePermissions.Add(new RolePermission { RoleName = "MainBranchAdmin", Permission = p });

        // SuperAdmin - all permissions
        foreach (var p in allPermissions)
            rolePermissions.Add(new RolePermission { RoleName = "SuperAdmin", Permission = p });

        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();
    }
}
