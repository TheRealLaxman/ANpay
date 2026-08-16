using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class InvestmentTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InvestmentId { get; set; }

    [ForeignKey("InvestmentId")]
    public Investment Investment { get; set; } = null!;

    [Required]
    public InvestmentTransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum InvestmentTransactionType
{
    Deposit = 0,
    Withdrawal = 1,
    Interest = 2,
    Penalty = 3,
    Bonus = 4
}
