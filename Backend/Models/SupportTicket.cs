using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    WaitingForCustomer = 2,
    Escalated = 3,
    Resolved = 4,
    Closed = 5
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum TicketCategory
{
    General = 0,
    TransactionIssue = 1,
    AccountIssue = 2,
    KycIssue = 3,
    PaymentProblem = 4,
    RefundRequest = 5,
    SecurityConcern = 6,
    Other = 7
}

public class SupportTicket
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public string? AssignedToId { get; set; }

    public ApplicationUser? AssignedTo { get; set; }

    [Required]
    [MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public TicketCategory Category { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public Guid? RelatedTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

public class TicketMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TicketId { get; set; }

    public SupportTicket Ticket { get; set; } = null!;

    [Required]
    public string SenderId { get; set; } = string.Empty;

    public ApplicationUser Sender { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public bool IsInternal { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
