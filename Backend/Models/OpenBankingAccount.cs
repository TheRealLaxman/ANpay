using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class OpenBankingAccount
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string BankCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    public AccountType AccountType { get; set; } = AccountType.Savings;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "NGN";

    public OpenBankingAccountStatus Status { get; set; } = OpenBankingAccountStatus.Connected;

    [MaxLength(500)]
    public string? AccessToken { get; set; }

    [MaxLength(500)]
    public string? RefreshToken { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public bool EnableAutoSync { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AccountType
{
    Savings = 0,
    Current = 1,
    FixedDeposit = 2,
    Wallet = 3
}

public enum OpenBankingAccountStatus
{
    Connected = 0,
    Disconnected = 1,
    Error = 2,
    Expired = 3
}
