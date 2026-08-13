using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class FraudAlert
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    public FraudAlertType AlertType { get; set; }

    public FraudAlertStatus Status { get; set; } = FraudAlertStatus.Open;

    public int RiskScore { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    [MaxLength(2000)]
    public string? Resolution { get; set; }

    public string? AssignedToId { get; set; }

    [ForeignKey("AssignedToId")]
    public ApplicationUser? AssignedTo { get; set; }

    public Guid? RelatedTransactionId { get; set; }

    [MaxLength(100)]
    public string? IPAddress { get; set; }

    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public class RiskScore
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public string EntityId { get; set; } = string.Empty;

    public int Score { get; set; }

    [MaxLength(2000)]
    public string Factors { get; set; } = string.Empty;

    public RiskLevel Level { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public enum FraudAlertType
{
    VelocityExceeded = 0,
    LargeTransaction = 1,
    RapidTransfers = 2,
    UnusualIP = 3,
    UnusualDevice = 4,
    FailedTransactionPattern = 5,
    SuspiciousLogin = 6,
    HighRiskTransaction = 7,
    MultipleAccounts = 8,
    SanctionsMatch = 9
}

public enum FraudAlertStatus
{
    Open = 0,
    UnderReview = 1,
    Confirmed = 2,
    FalsePositive = 3,
    Resolved = 4,
    Escalated = 5
}

public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
