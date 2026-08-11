using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum CryptoNetwork
{
    Ethereum = 0,
    Tron = 1,
    BnbSmartChain = 2,
    Bitcoin = 3,
    Solana = 4
}

public enum CryptoAsset
{
    BTC = 0,
    ETH = 1,
    USDT = 2,
    USDC = 3,
    BNB = 4,
    SOL = 5
}

public class CryptoWallet
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public CryptoAsset Asset { get; set; }

    [Required]
    public CryptoNetwork Network { get; set; }

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DerivationPath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CryptoTransaction> Transactions { get; set; } = new List<CryptoTransaction>();
}

public class CryptoTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CryptoWalletId { get; set; }

    [ForeignKey("CryptoWalletId")]
    public CryptoWallet CryptoWallet { get; set; } = null!;

    [Required]
    public CryptoTransactionType Type { get; set; }

    [Required]
    public CryptoAsset Asset { get; set; }

    [Required]
    public CryptoNetwork Network { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetworkFee { get; set; }

    [MaxLength(200)]
    public string? TxHash { get; set; }

    [MaxLength(200)]
    public string? FromAddress { get; set; }

    [MaxLength(200)]
    public string? ToAddress { get; set; }

    public int Confirmations { get; set; } = 0;

    public int RequiredConfirmations { get; set; } = 12;

    public CryptoTransactionStatus Status { get; set; } = CryptoTransactionStatus.Pending;

    [MaxLength(500)]
    public string? BlockExplorerUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public enum CryptoTransactionType
{
    Deposit = 0,
    Withdrawal = 1,
    Send = 2,
    Receive = 3
}

public enum CryptoTransactionStatus
{
    Pending = 0,
    Confirming = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public class CryptoNetworkConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public CryptoNetwork Network { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    public int ChainId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal GasPriceGwei { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal AverageTxFee { get; set; }

    public int AverageBlockTimeSeconds { get; set; }

    public int DefaultConfirmations { get; set; } = 12;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string ExplorerBaseUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
