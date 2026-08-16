using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class LoyaltyTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public LoyaltyTransactionType Type { get; set; }

    public int Points { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid? TransactionId { get; set; }

    [ForeignKey("TransactionId")]
    public Transaction? Transaction { get; set; }

    public Guid? WalletId { get; set; }

    [ForeignKey("WalletId")]
    public Wallet? Wallet { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum LoyaltyTransactionType
{
    Earned = 0,
    Redeemed = 1,
    Expired = 2,
    Bonus = 3,
    Referral = 4,
    Cashback = 5
}
