using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class AiChat
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string SessionTitle { get; set; } = "New Chat";

    public AiChatStatus Status { get; set; } = AiChatStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}

public enum AiChatStatus
{
    Active = 0,
    Archived = 1,
    Deleted = 2
}
