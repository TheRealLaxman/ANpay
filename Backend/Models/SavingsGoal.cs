using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class SavingsGoal
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
    [MaxLength(100)]
    public string GoalName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? GoalDescription { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAmount { get; set; }

    [NotMapped]
    public decimal ProgressPercentage => TargetAmount > 0 ? (CurrentAmount / TargetAmount) * 100 : 0;

    [NotMapped]
    public decimal RemainingAmount => TargetAmount - CurrentAmount;

    public DateTime TargetDate { get; set; }

    public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.Active;

    public bool AutoSave { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AutoSaveAmount { get; set; } = 0;

    public SavingsGoalFrequency AutoSaveFrequency { get; set; } = SavingsGoalFrequency.Weekly;

    [MaxLength(200)]
    public string? IconUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum SavingsGoalStatus
{
    Active = 0,
    Completed = 1,
    Paused = 2,
    Cancelled = 3,
    Withdrawn = 4
}

public enum SavingsGoalFrequency
{
    Daily = 0,
    Weekly = 1,
    Biweekly = 2,
    Monthly = 3
}
