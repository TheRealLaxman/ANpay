using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class AiMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid AiChatId { get; set; }

    [ForeignKey("AiChatId")]
    public AiChat AiChat { get; set; } = null!;

    [Required]
    public AiMessageRole Role { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Intent { get; set; } // Detected intent

    [MaxLength(200)]
    public string? EntityType { get; set; } // e.g., "transaction", "wallet", "bill"

    [MaxLength(200)]
    public string? EntityId { get; set; } // Related entity ID

    public bool IsHelpful { get; set; } = false;

    public bool IsFlagged { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AiMessageRole
{
    User = 0,
    Assistant = 1,
    System = 2
}
