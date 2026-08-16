using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class PosTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PosDeviceId { get; set; }

    [ForeignKey("PosDeviceId")]
    public PosDevice PosDevice { get; set; } = null!;

    [Required]
    public Guid? WalletId { get; set; }

    [ForeignKey("WalletId")]
    public Wallet? Wallet { get; set; }

    [Required]
    public PosTransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "NGN";

    public PosTransactionStatus Status { get; set; } = PosTransactionStatus.Pending;

    [MaxLength(100)]
    public string? AuthorizationCode { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public bool IsTapToPay { get; set; } = false;

    [MaxLength(100)]
    public string? CardLast4 { get; set; }

    [MaxLength(50)]
    public string? CardType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum PosTransactionType
{
    Sale = 0,
    Refund = 1,
    Void = 2,
    BalanceInquiry = 3
}

public enum PosTransactionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Declined = 3,
    Voided = 4
}
