namespace ANpay.Api.Services.Crypto;

public interface IBlockchainService
{
    Task<BlockchainWalletInfo> GenerateWalletAsync(string? label = null);
    Task<decimal> GetBalanceAsync(string address);
    Task<BlockchainTransaction?> GetTransactionAsync(string txHash);
    Task<List<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 50);
    Task<BlockchainFeeEstimate> EstimateFeeAsync();
    Task<string> SendTransactionAsync(string fromAddress, string toAddress, decimal amount, decimal fee);
    Task<List<Utxo>> GetUtxosAsync(string address);
    Task<int> GetConfirmationsAsync(string txHash);
    Task<bool> ValidateAddressAsync(string address);
}

public class BlockchainWalletInfo
{
    public string Address { get; set; } = string.Empty;
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string? Mnemonic { get; set; }
    public string Network { get; set; } = string.Empty;
}

public class BlockchainTransaction
{
    public string TxHash { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public int Confirmations { get; set; }
    public DateTime? BlockTime { get; set; }
    public bool IsConfirmed => Confirmations > 0;
    public string Status { get; set; } = "pending";
}

public class BlockchainFeeEstimate
{
    public decimal Low { get; set; }
    public decimal Medium { get; set; }
    public decimal High { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class Utxo
{
    public string TxHash { get; set; } = string.Empty;
    public int Vout { get; set; }
    public decimal Amount { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsConfirmed { get; set; }
}
