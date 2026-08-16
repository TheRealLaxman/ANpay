using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class CreditScoreFactor
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CreditScoreId { get; set; }

    [ForeignKey("CreditScoreId")]
    public CreditScore CreditScore { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FactorName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal Weight { get; set; } // Percentage weight

    [Column(TypeName = "decimal(5,2)")]
    public decimal Impact { get; set; } // Positive or negative impact

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
