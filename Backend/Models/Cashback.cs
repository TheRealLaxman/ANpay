using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Cashback
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public Guid WalletId { get; set; }

    [ForeignKey("WalletId")]
    public Wallet Wallet { get; set; } = null!;

    [Required]
    public Guid TransactionId { get; set; }

    [ForeignKey("TransactionId")]
    public Transaction Transaction { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashbackAmount { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CashbackPercentage { get; set; }

    public CashbackStatus Status { get; set; } = CashbackStatus.Pending;

    public CashbackType Type { get; set; } = CashbackType.Transaction;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CreditedAt { get; set; }
}

public enum CashbackStatus
{
    Pending = 0,
    Credited = 1,
    Failed = 2,
    Expired = 3
}

public enum CashbackType
{
    Transaction = 0,
    Referral = 1,
    Promotion = 2,
    Bonus = 3
}
