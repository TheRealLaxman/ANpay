using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class ScheduledTransfer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public Guid SourceWalletId { get; set; }

    [Required]
    public Guid DestinationWalletId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.Monthly;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int DayOfMonth { get; set; } = 1;

    public DayOfWeek? DayOfWeek { get; set; }

    public DateTime NextExecutionDate { get; set; }

    public int ExecutionCount { get; set; } = 0;

    public int MaxExecutions { get; set; } = 0;

    public ScheduledTransferStatus Status { get; set; } = ScheduledTransferStatus.Active;

    [MaxLength(500)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }

    [ForeignKey("SourceWalletId")]
    public Wallet? SourceWallet { get; set; }

    [ForeignKey("DestinationWalletId")]
    public Wallet? DestinationWallet { get; set; }
}

public enum RecurrenceType
{
    Weekly = 0,
    Biweekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}

public enum ScheduledTransferStatus
{
    Active = 0,
    Paused = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
