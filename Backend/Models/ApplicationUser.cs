using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ANpay.Api.Models;

public enum AppUserRole
{
    Customer = 0,
    Official = 1,
    BranchAdmin = 2,
    MainBranchAdmin = 3,
    SuperAdmin = 4
}

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public AppUserRole Role { get; set; } = AppUserRole.Customer;
    public Guid? BranchId { get; set; }
    public string? TransactionPinHash { get; set; }
    public bool IsTransactionPinSet { get; set; } = false;
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
    public EmployeeProfile? EmployeeProfile { get; set; }
    public KycProfile? KycProfile { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
