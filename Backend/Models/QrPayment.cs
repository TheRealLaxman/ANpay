using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum QrCodeType
{
    CustomerPay = 0,
    MerchantPayment = 1,
    BranchDeposit = 2,
    WalletAddress = 3
}

public enum QrCodeStatus
{
    Active = 0,
    Used = 1,
    Expired = 2,
    Cancelled = 3
}

public class QrCode
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string CreatedById { get; set; } = string.Empty;

    [ForeignKey("CreatedById")]
    public ApplicationUser CreatedBy { get; set; } = null!;

    [Required]
    public QrCodeType Type { get; set; }

    [Required]
    [MaxLength(500)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Payload { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedAmount { get; set; }

    public Guid? WalletId { get; set; }

    public Guid? MerchantId { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public QrCodeStatus Status { get; set; } = QrCodeStatus.Active;

    public DateTime? ExpiresAt { get; set; }

    public int UsageLimit { get; set; } = 1;

    public int UsageCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentLink
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string CreatedById { get; set; } = string.Empty;

    [ForeignKey("CreatedById")]
    public ApplicationUser CreatedBy { get; set; } = null!;

    public Guid? MerchantId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";

    [Required]
    [MaxLength(500)]
    public string LinkUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int UsageCount { get; set; } = 0;

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
