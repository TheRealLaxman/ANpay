using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Transaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid WalletId { get; set; }
    
    [ForeignKey("WalletId")]
    public Wallet Wallet { get; set; } = null!;
    
    [Required]
    public TransactionType Type { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceBefore { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }
    
    public Guid? DestinationWalletId { get; set; }
    
    [ForeignKey("DestinationWalletId")]
    public Wallet? DestinationWallet { get; set; }
    
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    [MaxLength(20)]
    public string Channel { get; set; } = "App";

    public Guid? BranchId { get; set; }

    [MaxLength(100)]
    public string? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? AuthorizationInfo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TransactionType
{
    Deposit = 0,
    Withdrawal = 1,
    TransferIn = 2,
    TransferOut = 3,
    Payment = 4,
    Refund = 5
}

public enum TransactionStatus
{
    Initiated = 0,
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Reversed = 6,
    Refunded = 7
}

public enum TransactionChannel
{
    App = 0,
    Web = 1,
    API = 2,
    Branch = 3,
    USSD = 4
}
