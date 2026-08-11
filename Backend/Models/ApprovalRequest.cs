using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Expired = 3
}

public enum ApprovalType
{
    LargeWithdrawal = 0,
    LargeTransfer = 1,
    BalanceAdjustment = 2,
    KycApproval = 3,
    UserSuspension = 4,
    BranchSettings = 5
}

public class ApprovalRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public ApprovalType Type { get; set; }

    [Required]
    public string RequestedById { get; set; } = string.Empty;

    public ApplicationUser RequestedBy { get; set; } = null!;

    public string? ApprovedById { get; set; }

    public ApplicationUser? ApprovedBy { get; set; }

    [MaxLength(100)]
    public string TargetEntity { get; set; } = string.Empty;

    public Guid? TargetEntityId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }
}
