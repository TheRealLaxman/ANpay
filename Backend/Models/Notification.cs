using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public enum NotificationType
{
    TransactionCompleted = 0,
    TransactionFailed = 1,
    MoneyReceived = 2,
    MoneySent = 3,
    KycUpdated = 4,
    KycApproved = 5,
    KycRejected = 6,
    LoginAlert = 7,
    SecurityAlert = 8,
    PasswordChanged = 9,
    AccountSuspended = 10,
    ApprovalRequired = 11,
    ApprovalCompleted = 12,
    SupportTicket = 13,
    SystemAnnouncement = 14,
    LimitReached = 15
}

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    [MaxLength(200)]
    public string? ActionUrl { get; set; }

    public Guid? RelatedEntityId { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
