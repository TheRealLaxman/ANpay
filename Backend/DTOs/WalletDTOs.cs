using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.DTOs;

public class CreateWalletDto
{
    [Required(ErrorMessage = "Wallet name is required")]
    [MaxLength(100)]
    public string WalletName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Currency is required")]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";
}

public class WalletDto
{
    public Guid Id { get; set; }
    public string WalletName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TransferDto
{
    [Required]
    public Guid SourceWalletId { get; set; }

    [Required]
    public Guid DestinationWalletId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}

public class DepositDto
{
    [Required]
    public Guid WalletId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}

public class WithdrawDto
{
    [Required]
    public Guid WalletId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}
