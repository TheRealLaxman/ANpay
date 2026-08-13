using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public class SystemSetting
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
