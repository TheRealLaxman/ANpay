using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum LedgerAccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4
}

public class LedgerAccount
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public LedgerAccountType Type { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid? ParentAccountId { get; set; }

    [ForeignKey("ParentAccountId")]
    public LedgerAccount? ParentAccount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LedgerEntry> DebitEntries { get; set; } = new List<LedgerEntry>();
    public ICollection<LedgerEntry> CreditEntries { get; set; } = new List<LedgerEntry>();
}

public class LedgerEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TransactionId { get; set; }

    public Transaction? Transaction { get; set; }

    [Required]
    public Guid DebitAccountId { get; set; }

    [ForeignKey("DebitAccountId")]
    public LedgerAccount DebitAccount { get; set; } = null!;

    [Required]
    public Guid CreditAccountId { get; set; }

    [ForeignKey("CreditAccountId")]
    public LedgerAccount CreditAccount { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string Currency { get; set; } = "NGN";

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
