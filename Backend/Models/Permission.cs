using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public enum Permission
{
    // User management
    ViewUsers = 1,
    CreateUsers = 2,
    EditUsers = 3,
    DeleteUsers = 4,
    SuspendUsers = 5,

    // Branch management
    ViewBranches = 10,
    CreateBranches = 11,
    EditBranches = 12,
    DeleteBranches = 13,

    // Employee management
    ViewEmployees = 20,
    CreateEmployees = 21,
    EditEmployees = 22,
    DeleteEmployees = 23,

    // Wallet operations
    ViewWallets = 30,
    CreateWallets = 31,
    FreezeWallets = 32,
    AdjustBalance = 33,

    // Transaction operations
    ViewTransactions = 40,
    CreateDeposit = 41,
    CreateWithdrawal = 42,
    CreateTransfer = 43,
    ApproveTransactions = 44,
    ReverseTransactions = 45,

    // KYC
    ViewKyc = 50,
    SubmitKyc = 51,
    ReviewKyc = 52,
    ApproveKyc = 53,
    RejectKyc = 54,

    // Reports
    ViewReports = 60,
    ExportReports = 61,

    // System
    ManageSystemSettings = 70,
    ManageFees = 71,
    ManageLimits = 72,
    ViewAuditLogs = 73,
    ManageExchangeRates = 74,

    // Support
    ViewTickets = 80,
    RespondTickets = 81,
    CloseTickets = 82,

    // Notifications
    SendNotifications = 90,
    ViewNotifications = 91,

    // Cash management
    ViewCashBalance = 100,
    AdjustCash = 101,
    PerformReconciliation = 102
}

public class RolePermission
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public Permission Permission { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
