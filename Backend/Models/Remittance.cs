using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Remittance
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string SenderUserId { get; set; } = string.Empty;

    [ForeignKey("SenderUserId")]
    public ApplicationUser SenderUser { get; set; } = null!;

    [Required]
    public Guid SenderWalletId { get; set; }

    [ForeignKey("SenderWalletId")]
    public Wallet SenderWallet { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RecipientCountry { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RecipientBankCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RecipientAccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string RecipientCurrency { get; set; } = "USD";

    [Required]
    [MaxLength(100)]
    public string? RecipientBankName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SendAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string SendCurrency { get; set; } = "NGN";

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceiveAmount { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    public RemittanceStatus Status { get; set; } = RemittanceStatus.Pending;

    [MaxLength(100)]
    public string? ExternalReference { get; set; }

    [MaxLength(500)]
    public string? TrackingNumber { get; set; }

    public RemittancePurpose Purpose { get; set; } = RemittancePurpose.FamilySupport;

    [MaxLength(500)]
    public string? PurposeDescription { get; set; }

    public bool IsCompliant { get; set; } = true;

    [MaxLength(500)]
    public string? ComplianceNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? EstimatedDeliveryDate { get; set; }
}

public enum RemittanceStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    OnHold = 5
}

public enum RemittancePurpose
{
    FamilySupport = 0,
    Education = 1,
    Medical = 2,
    Business = 3,
    Investment = 4,
    Gift = 5,
    Other = 6
}