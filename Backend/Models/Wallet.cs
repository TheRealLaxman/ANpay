using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Wallet
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string WalletName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PendingBalance { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FrozenBalance { get; set; } = 0;

    [NotMapped]
    public decimal AvailableBalance => Balance - FrozenBalance;

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = new byte[8];

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
