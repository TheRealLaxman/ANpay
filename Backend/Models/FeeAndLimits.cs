using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum FeeType
{
    Percentage = 0,
    Fixed = 1
}

public enum FeeAppliesTo
{
    Deposit = 0,
    Withdrawal = 1,
    Transfer = 2,
    Exchange = 3
}

public class Fee
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public FeeType Type { get; set; }

    [Required]
    public FeeAppliesTo AppliesTo { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Value { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxAmount { get; set; } = decimal.MaxValue;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinFee { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxFee { get; set; } = decimal.MaxValue;

    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TransactionLimitType
{
    DailyDeposit = 0,
    DailyWithdrawal = 1,
    DailyTransfer = 2,
    MonthlyDeposit = 3,
    MonthlyWithdrawal = 4,
    MonthlyTransfer = 5,
    SingleTransaction = 6
}

public class TransactionLimit
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public TransactionLimitType LimitType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LimitAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
