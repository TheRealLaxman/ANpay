using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class ApiKey
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Secret { get; set; }

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.Read;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Permissions { get; set; } // JSON array of permissions

    public DateTime? LastUsedAt { get; set; }

    public int UsageCount { get; set; } = 0;

    public int MaxUsagePerDay { get; set; } = 1000;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddYears(1);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ApiKeyScope
{
    Read = 0,
    Write = 1,
    Admin = 2,
    FullAccess = 3
}
