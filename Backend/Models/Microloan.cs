using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Microloan
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

    [Column(TypeName = "decimal(18,2)")]
    public decimal PrincipalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DisbursedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InterestAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalRepayable { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; }

    public int TenureDays { get; set; } = 30;

    public int RepaymentFrequencyDays { get; set; } = 7;

    public MicroloanStatus Status { get; set; } = MicroloanStatus.Applied;

    public MicroloanPurpose Purpose { get; set; } = MicroloanPurpose.Personal;

    [MaxLength(500)]
    public string? PurposeDescription { get; set; }

    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

    public DateTime? DisbursedDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public int DaysOverdue { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PenaltyAmount { get; set; } = 0;

    public int CreditScoreAtApplication { get; set; } = 0;

    public bool AutoDebitEnabled { get; set; } = true;

    public ICollection<MicroloanRepayment> Repayments { get; set; } = new List<MicroloanRepayment>();
}

public enum MicroloanStatus
{
    Applied = 0,
    UnderReview = 1,
    Approved = 2,
    Disbursed = 3,
    Repaying = 4,
    Completed = 5,
    Defaulted = 6,
    Rejected = 7
}

public enum MicroloanPurpose
{
    Personal = 0,
    Business = 1,
    Education = 2,
    Medical = 3,
    Emergency = 4,
    Other = 5
}
