using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Investment
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
    public InvestmentType Type { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PrincipalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InterestEarned { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRate { get; set; }

    public int TenureDays { get; set; } = 90;

    public InvestmentStatus Status { get; set; } = InvestmentStatus.Active;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime MaturityDate { get; set; }

    public bool AutoRenew { get; set; } = false;

    public bool IsLocked { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal EarlyWithdrawalPenalty { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvestmentTransaction> Transactions { get; set; } = new List<InvestmentTransaction>();
}

public enum InvestmentType
{
    FixedDeposit = 0,
    SavingsGoal = 1,
    TreasuryBill = 2,
    MoneyMarket = 3,
    MutualFund = 4
}

public enum InvestmentStatus
{
    Active = 0,
    Matured = 1,
    Withdrawn = 2,
    Cancelled = 3,
    Defaulted = 4
}
