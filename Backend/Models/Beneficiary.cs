using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class Beneficiary
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Nickname { get; set; } = string.Empty;

    [Required]
    public Guid WalletId { get; set; }

    [ForeignKey("WalletId")]
    public Wallet Wallet { get; set; } = null!;

    [MaxLength(200)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
