using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class LoginHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [MaxLength(45)]
    public string? IPAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }

    public bool IsSuccess { get; set; }

    [MaxLength(200)]
    public string? FailureReason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class TrustedDevice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string DeviceName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    [MaxLength(45)]
    public string? LastIPAddress { get; set; }

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}
