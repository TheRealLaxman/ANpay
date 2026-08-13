using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Entity { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    [MaxLength(500)]
    public string OldValues { get; set; } = string.Empty;

    [MaxLength(500)]
    public string NewValues { get; set; } = string.Empty;

    [MaxLength(100)]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(500)]
    public string UserAgent { get; set; } = string.Empty;

    public bool IsSuccess { get; set; } = true;

    [MaxLength(500)]
    public string ErrorMessage { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Hash { get; set; }

    [MaxLength(128)]
    public string? PreviousHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
