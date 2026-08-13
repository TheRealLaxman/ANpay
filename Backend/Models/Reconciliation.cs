using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class ReconciliationRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = string.Empty;

    [Required]
    public ReconciliationType Type { get; set; }

    [Required]
    public DateTime PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SystemBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExternalBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Difference => SystemBalance - ExternalBalance;

    public bool IsMatched { get; set; }

    [MaxLength(1000)]
    public string? DiscrepancyDetails { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Pending;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? ReviewedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public ICollection<ReconciliationTransaction> ReconciliationTransactions { get; set; } = new List<ReconciliationTransaction>();
}

public class ReconciliationTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ReconciliationRecordId { get; set; }

    [ForeignKey("ReconciliationRecordId")]
    public ReconciliationRecord ReconciliationRecord { get; set; } = null!;

    public Guid? TransactionId { get; set; }

    [MaxLength(100)]
    public string? ExternalReference { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public bool IsMatched { get; set; }

    [MaxLength(500)]
    public string? MismatchReason { get; set; }
}

public enum ReconciliationType
{
    WalletToBank = 0,
    WalletToPaymentGateway = 1,
    WalletToBlockchain = 2,
    BranchCash = 3,
    MerchantSettlement = 4
}

public enum ReconciliationStatus
{
    Pending = 0,
    InProgress = 1,
    Matched = 2,
    DiscrepancyFound = 3,
    Resolved = 4,
    Escalated = 5
}
