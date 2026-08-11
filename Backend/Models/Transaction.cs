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
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3
}
