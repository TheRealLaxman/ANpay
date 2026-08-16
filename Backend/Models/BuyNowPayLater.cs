using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class BuyNowPayLater
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
    public Guid? MerchantId { get; set; }

    [ForeignKey("MerchantId")]
    public Merchant? Merchant { get; set; }

    [Required]
    public Guid? MerchantPaymentId { get; set; }

    [ForeignKey("MerchantPaymentId")]
    public MerchantPayment? MerchantPayment { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DownPayment { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    public int TotalInstallments { get; set; } = 4;

    public int PaidInstallments { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal InstallmentAmount { get; set; }

    public BnplFrequency Frequency { get; set; } = BnplFrequency.Weekly;

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; } = 0;

    public BnplStatus Status { get; set; } = BnplStatus.Pending;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public DateTime? NextPaymentDate { get; set; }

    public int OverdueInstallments { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BnplInstallment> Installments { get; set; } = new List<BnplInstallment>();
}

public enum BnplFrequency
{
    Weekly = 0,
    Biweekly = 1,
    Monthly = 2
}

public enum BnplStatus
{
    Pending = 0,
    Active = 1,
    Completed = 2,
    Defaulted = 3,
    Cancelled = 4,
    Paused = 5
}
