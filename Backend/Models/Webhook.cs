using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Webhook
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Secret { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Events { get; set; } // JSON array of event types

    public WebhookStatus Status { get; set; } = WebhookStatus.Active;

    public int RetryCount { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 30;

    public DateTime? LastTriggeredAt { get; set; }

    public int SuccessCount { get; set; } = 0;

    public int FailureCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

public enum WebhookStatus
{
    Active = 0,
    Paused = 1,
    Failed = 2
}
