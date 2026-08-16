using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class VirtualCard
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public Guid WalletId { get; set; }

    [ForeignKey("WalletId")]
    public Wallet Wallet { get; set; } = null!;

    [Required]
    [MaxLength(19)]
    public string CardNumber { get; set; } = string.Empty; // Masked: ****-****-****-1234

    [Required]
    [MaxLength(100)]
    public string CardToken { get; set; } = string.Empty; // Token from card provider

    [Required]
    [MaxLength(100)]
    public string CardHolderName { get; set; } = string.Empty;

    [Required]
    [MaxLength(5)]
    public string ExpiryMonth { get; set; } = string.Empty; // MM

    [Required]
    [MaxLength(4)]
    public string ExpiryYear { get; set; } = string.Empty; // YYYY

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    public VirtualCardType CardType { get; set; } = VirtualCardType.Standard;

    public VirtualCardStatus Status { get; set; } = VirtualCardStatus.Active;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyLimit { get; set; } = 1000;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyLimit { get; set; } = 10000;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentDaySpent { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentMonthSpent { get; set; } = 0;

    public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

    public bool IsLocked { get; set; } = false;

    public bool AllowOnlinePayments { get; set; } = true;

    public bool AllowAtmWithdrawals { get; set; } = false;

    public bool AllowPosTransactions { get; set; } = true;

    [MaxLength(100)]
    public string? FingerprintId { get; set; } // For biometric auth

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public ICollection<VirtualCardTransaction> Transactions { get; set; } = new List<VirtualCardTransaction>();
}

public enum VirtualCardType
{
    Standard = 0,
    Premium = 1,
    Business = 2,
    Disposable = 3
}

public enum VirtualCardStatus
{
    Active = 0,
    Frozen = 1,
    Closed = 2,
    Expired = 3,
    Blocked = 4
}
