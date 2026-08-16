using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class CreditScore
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    public int Score { get; set; } = 0; // 0-850 range like FICO

    public CreditRating Rating { get; set; } = CreditRating.New;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaximumCreditLimit { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentCreditUsed { get; set; } = 0;

    [NotMapped]
    public decimal AvailableCredit => MaximumCreditLimit - CurrentCreditUsed;

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; } = 0;

    public int TotalTransactions { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalVolume { get; set; } = 0;

    public int AccountAgeDays { get; set; } = 0;

    public int SuccessfulTransfers { get; set; } = 0;

    public int FailedTransfers { get; set; } = 0;

    public int DisputesCount { get; set; } = 0;

    public int OnTimePayments { get; set; } = 0;

    public int LatePayments { get; set; } = 0;

    public bool HasKyc { get; set; } = false;

    public bool HasEmploymentInfo { get; set; } = false;

    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CreditScoreFactor> Factors { get; set; } = new List<CreditScoreFactor>();
}

public enum CreditRating
{
    New = 0,        // No score yet
    Poor = 1,       // 300-579
    Fair = 2,       // 580-669
    Good = 3,       // 670-739
    VeryGood = 4,   // 740-799
    Exceptional = 5 // 800-850
}
