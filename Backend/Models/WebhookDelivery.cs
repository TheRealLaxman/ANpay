using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class WebhookDelivery
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid WebhookId { get; set; }

    [ForeignKey("WebhookId")]
    public Webhook Webhook { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Payload { get; set; }

    public int StatusCode { get; set; } = 0;

    [MaxLength(1000)]
    public string? ResponseBody { get; set; }

    public bool IsSuccess { get; set; } = false;

    public int AttemptNumber { get; set; } = 1;

    public DateTime? DeliveredAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2
}
