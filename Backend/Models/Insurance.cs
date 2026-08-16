using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Insurance
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
    public InsuranceType Type { get; set; }

    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PremiumAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CoverageAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "NGN";

    public InsuranceFrequency Frequency { get; set; } = InsuranceFrequency.Monthly;

    public InsuranceStatus Status { get; set; } = InsuranceStatus.Active;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; }

    public DateTime? LastPaymentDate { get; set; }

    public DateTime? NextPaymentDate { get; set; }

    public int TotalClaims { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalClaimedAmount { get; set; } = 0;

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InsuranceClaim> Claims { get; set; } = new List<InsuranceClaim>();
}

public enum InsuranceType
{
    Health = 0,
    Life = 1,
    Vehicle = 2,
    Travel = 3,
    Device = 4,
    Property = 5,
    Business = 6
}

public enum InsuranceFrequency
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}

public enum InsuranceStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2,
    Suspended = 3,
    Claimed = 4
}
