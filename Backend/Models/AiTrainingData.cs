using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class AiTrainingData
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Answer { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Keywords { get; set; }

    public int MinRole { get; set; } = 0; // Minimum role level needed

    public bool IsGlobal { get; set; } = true; // Available to all roles

    public int UsageCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
