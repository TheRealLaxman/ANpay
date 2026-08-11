namespace ANpay.Api.DTOs;

public class CreateWalletDto
{
    public string WalletName { get; set; } = string.Empty;
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
    public Guid SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class DepositDto
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class WithdrawDto
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
