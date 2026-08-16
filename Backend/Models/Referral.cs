using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Referral
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string ReferrerUserId { get; set; } = string.Empty;

    [ForeignKey("ReferrerUserId")]
    public ApplicationUser ReferrerUser { get; set; } = null!;

    [Required]
    public string ReferredUserId { get; set; } = string.Empty;

    [ForeignKey("ReferredUserId")]
    public ApplicationUser ReferredUser { get; set; } = null!;

    public ReferralStatus Status { get; set; } = ReferralStatus.Pending;

    public int ReferrerRewardPoints { get; set; } = 0;

    public int ReferredRewardPoints { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReferrerCashReward { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum ReferralStatus
{
    Pending = 0,
    Completed = 1,
    Expired = 2,
    Cancelled = 3
}
