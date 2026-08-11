using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public enum KycStatus
{
    NotStarted = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5
}

public enum KycLevel
{
    None = 0,
    Basic = 1,
    Full = 2,
    Business = 3
}

public class KycProfile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string IdType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string IdNumber { get; set; } = string.Empty;

    public KycStatus Status { get; set; } = KycStatus.NotStarted;

    public KycLevel Level { get; set; } = KycLevel.None;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string ReviewNotes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KycDocument> Documents { get; set; } = new List<KycDocument>();
}

public class KycDocument
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid KycProfileId { get; set; }

    public KycProfile KycProfile { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string DocumentUrl { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
