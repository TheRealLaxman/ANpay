using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Dispute
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    public Guid? TransactionId { get; set; }

    [ForeignKey("TransactionId")]
    public Transaction? Transaction { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public DisputeCategory Category { get; set; }

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    public DisputePriority Priority { get; set; } = DisputePriority.Medium;

    [MaxLength(2000)]
    public string? Resolution { get; set; }

    public string? AssignedToId { get; set; }

    [ForeignKey("AssignedToId")]
    public ApplicationUser? AssignedTo { get; set; }

    public decimal? RefundAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
}

public class DisputeMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DisputeId { get; set; }

    [ForeignKey("DisputeId")]
    public Dispute Dispute { get; set; } = null!;

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [ForeignKey("SenderId")]
    public ApplicationUser Sender { get; set; } = null!;

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum DisputeCategory
{
    WrongTransfer = 0,
    UnauthorizedTransaction = 1,
    FailedPayment = 2,
    DuplicatePayment = 3,
    MerchantDispute = 4,
    CashWithdrawalProblem = 5,
    ServiceIssue = 6,
    Other = 7
}

public enum DisputeStatus
{
    Open = 0,
    UnderReview = 1,
    EvidenceRequired = 2,
    Escalated = 3,
    Resolved = 4,
    Rejected = 5,
    Closed = 6
}

public enum DisputePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}
