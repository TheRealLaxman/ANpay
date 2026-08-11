using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum MerchantStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}

public class Merchant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string BusinessType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string BusinessAddress { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(50)]
    public string TaxId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string RegistrationDocUrl { get; set; } = string.Empty;

    public MerchantStatus Status { get; set; } = MerchantStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyLimit { get; set; } = 1000000;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyLimit { get; set; } = 30000000;

    [Column(TypeName = "decimal(18,4)")]
    public decimal CommissionRate { get; set; } = 0.015m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public ICollection<MerchantPayment> Payments { get; set; } = new List<MerchantPayment>();
    public ICollection<MerchantSettlement> Settlements { get; set; } = new List<MerchantSettlement>();
}

public class MerchantPayment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MerchantId { get; set; }

    [ForeignKey("MerchantId")]
    public Merchant Merchant { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string OrderReference { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Commission { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";

    public string? CustomerId { get; set; }

    public MerchantPaymentStatus Status { get; set; } = MerchantPaymentStatus.Pending;

    public PaymentMethod Method { get; set; } = PaymentMethod.QrCode;

    [MaxLength(200)]
    public string? PaymentReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum MerchantPaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4
}

public enum PaymentMethod
{
    QrCode = 0,
    PaymentLink = 1,
    Api = 2,
    InStore = 3
}

public class MerchantSettlement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MerchantId { get; set; }

    [ForeignKey("MerchantId")]
    public Merchant Merchant { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Commission { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; }

    public int PaymentCount { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SettledAt { get; set; }
}

public enum SettlementStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
