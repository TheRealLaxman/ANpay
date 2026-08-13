using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public class IdempotencyKey
{
    [Key]
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public int StatusCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }
}
