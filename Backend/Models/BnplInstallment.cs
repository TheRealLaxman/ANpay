using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class BnplInstallment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BuyNowPayLaterId { get; set; }

    [ForeignKey("BuyNowPayLaterId")]
    public BuyNowPayLater BuyNowPayLater { get; set; } = null!;

    public int InstallmentNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PenaltyAmount { get; set; } = 0;

    public DateTime DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public BnplInstallmentStatus Status { get; set; } = BnplInstallmentStatus.Pending;

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    public bool IsOverdue => Status == BnplInstallmentStatus.Pending && DateTime.UtcNow > DueDate;
}

public enum BnplInstallmentStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2,
    Defaulted = 3,
    Waived = 4
}
