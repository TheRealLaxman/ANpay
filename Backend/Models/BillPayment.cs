using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class BillPayment
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
    public BillCategory Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string BillerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CustomerReference { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "NGN";

    public BillPaymentStatus Status { get; set; } = BillPaymentStatus.Pending;

    [MaxLength(200)]
    public string? TransactionId { get; set; }

    [MaxLength(500)]
    public string? PaymentReference { get; set; }

    [MaxLength(500)]
    public string? ResponseMessage { get; set; }

    [MaxLength(100)]
    public string? Channel { get; set; } = "App";

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [MaxLength(100)]
    public string? CustomerAccountNumber { get; set; }

    [MaxLength(200)]
    public string? TokenOrPin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum BillCategory
{
    Electricity = 0,
    Airtime = 1,
    Data = 2,
    CableTV = 3,
    Internet = 4,
    Water = 5,
    Government = 6,
    Education = 7,
    Insurance = 8,
    Subscription = 9
}

public enum BillPaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5
}
