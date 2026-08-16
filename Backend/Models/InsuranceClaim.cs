using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class InsuranceClaim
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InsuranceId { get; set; }

    [ForeignKey("InsuranceId")]
    public Insurance Insurance { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string ClaimTitle { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string ClaimDescription { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ClaimAmount { get; set; }

    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    [MaxLength(500)]
    public string? SupportingDocuments { get; set; }

    [MaxLength(500)]
    public string? ResolutionNotes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedAmount { get; set; }

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedDate { get; set; }

    public DateTime? ResolvedDate { get; set; }
}

public enum ClaimStatus
{
    Submitted = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,
    PartiallyApproved = 4,
    Paid = 5
}
