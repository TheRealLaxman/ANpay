using System.Net.Http.Json;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ANpay.Api.Services.Crypto;

public class EthereumRpcService : IBlockchainService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EthereumRpcService> _logger;
    private readonly string _rpcUrl;

    // ERC-20 USDT/USDC contract addresses (Ethereum mainnet)
    private const string UsdtContract = "0xdAC17F958D2ee523a2206206994597C13D831ec7";
    private const string UsdcContract = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

    public EthereumRpcService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<EthereumRpcService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _rpcUrl = configuration["Crypto:EthereumRpcUrl"] ?? "https://mainnet.infura.io/v3/YOUR_KEY";
    }

    public async Task<BlockchainWalletInfo> GenerateWalletAsync(string? label = null)
    {
        try
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKey = ecdsa.ExportECPrivateKey();

            // Derive public key
            var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            var publicKeyBytes = new byte[64];
            var span = publicKey.AsSpan();
            // Skip the first 65 bytes (algorithm info) for uncompressed public key
            if (publicKey.Length > 65)
            {
                publicKey[65..].CopyTo(publicKeyBytes);
            }
            else
            {
                publicKey.CopyTo(publicKeyBytes);
            }

            // Hash the public key (keccak256 for Ethereum)
            var hash = Keccak256(publicKeyBytes);
            var addressBytes = hash[^20..]; // Last 20 bytes
            var address = "0x" + Convert.ToHexString(hash[^20..]).ToLower();

            return new BlockchainWalletInfo
            {
                Address = address,
                PublicKey = Convert.ToHexString(publicKey),
                PrivateKey = Convert.ToHexString(privateKey),
                Network = "Ethereum"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Ethereum wallet");
            throw;
        }
    }

    public async Task<decimal> GetBalanceAsync(string address)
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendEthRpcRequestAsync(client, "eth_getBalance", new object[] { address, "latest" });
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                var hexBalance = result.GetString()?.Replace("0x", "") ?? "0";
                var weiBalance = BigInteger.Parse(hexBalance, System.Globalization.NumberStyles.HexNumber);
                return (decimal)weiBalance / 1_000_000_000_000_000_000m; // Convert from wei to ETH
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Ethereum balance for {Address}", address);
            return 0;
        }
    }

    public async Task<decimal> GetTokenBalanceAsync(string address, string contractAddress)
    {
        try
        {
            using var client = CreateRpcClient();
            // balanceOf(address) function selector: 0x70a08231
            var data = "0x70a08231" + address.ToLower().Replace("0x", "").PadLeft(64, '0');
            var response = await SendEthRpcRequestAsync(client, "eth_call", new object[]
            {
                new { to = contractAddress, data },
                "latest"
            });

            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                var hexBalance = result.GetString()?.Replace("0x", "") ?? "0";
                if (BigInteger.TryParse(hexBalance, System.Globalization.NumberStyles.HexNumber, null, out var balance))
                {
                    // USDT and USDC have 6 decimals
                    return (decimal)balance / 1_000_000m;
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting token balance for {Address}", address);
            return 0;
        }
    }

    public async Task<BlockchainTransaction?> GetTransactionAsync(string txHash)
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendEthRpcRequestAsync(client, "eth_getTransactionByHash", new object[] { txHash });
            if (response == null || !response.Value.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                return null;

            var receiptResponse = await SendEthRpcRequestAsync(client, "eth_getTransactionReceipt", new object[] { txHash });
            var receipt = receiptResponse?.TryGetProperty("result", out var r) == true && r.ValueKind != JsonValueKind.Null ? r : default;

            var blockNumber = result.TryGetProperty("blockNumber", out var bn) ? bn.GetString() : null;
            var confirmations = 0;
            if (blockNumber != null)
            {
                var latestResponse = await SendEthRpcRequestAsync(client, "eth_blockNumber", Array.Empty<object>());
                var latestBlock = latestResponse?.TryGetProperty("result", out var lb) == true
                    ? Convert.ToInt64(lb.GetString()?.Replace("0x", ""), 16)
                    : 0;
                var txBlock = Convert.ToInt64(blockNumber.Replace("0x", ""), 16);
                confirmations = (int)(latestBlock - txBlock);
            }

            var amountHex = result.TryGetProperty("value", out var val) ? val.GetString()?.Replace("0x", "") : "0";
            var amount = BigInteger.Parse(amountHex ?? "0", System.Globalization.NumberStyles.HexNumber);

            return new BlockchainTransaction
            {
                TxHash = txHash,
                Amount = (decimal)amount / 1_000_000_000_000_000_000m,
                FromAddress = result.TryGetProperty("from", out var from) ? from.GetString() ?? "" : "",
                ToAddress = result.TryGetProperty("to", out var to) ? to.GetString() ?? "" : "",
                Confirmations = confirmations,
                Status = confirmations > 0 ? "confirmed" : "pending"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Ethereum transaction {TxHash}", txHash);
            return null;
        }
    }

    public async Task<List<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 50)
    {
        var transactions = new List<BlockchainTransaction>();
        try
        {
            using var client = CreateRpcClient();

            // Get latest block number
            var blockResponse = await SendEthRpcRequestAsync(client, "eth_blockNumber", Array.Empty<object>());
            if (blockResponse == null) return transactions;

            var latestBlock = Convert.ToInt64(blockResponse.Value.GetProperty("result").GetString()?.Replace("0x", ""), 16);

            // Scan recent blocks for transactions (limited to last 1000 blocks for performance)
            var startBlock = Math.Max(0, latestBlock - 1000);
            for (long i = latestBlock; i >= startBlock && transactions.Count < limit; i--)
            {
                var blockHex = "0x" + i.ToString("x");
                var blockResponse2 = await SendEthRpcRequestAsync(client, "eth_getBlockByNumber", new object[] { blockHex, true });

                if (blockResponse2 != null && blockResponse2.Value.TryGetProperty("result", out var block) && block.ValueKind != JsonValueKind.Null)
                {
                    var transactionsArray = block.TryGetProperty("transactions", out var txs) ? txs : default;
                    if (transactionsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tx in txs.EnumerateArray())
                        {
                            var from = tx.TryGetProperty("from", out var f) ? f.GetString() : "";
                            var to = tx.TryGetProperty("to", out var t) ? t.GetString() : "";

                            if (string.Equals(from, address, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(to, address, StringComparison.OrdinalIgnoreCase))
                            {
                                var valueHex = tx.TryGetProperty("value", out var v) ? v.GetString()?.Replace("0x", "") : "0";
                                var value = BigInteger.Parse(valueHex ?? "0", System.Globalization.NumberStyles.HexNumber);

                                transactions.Add(new BlockchainTransaction
                                {
                                    TxHash = tx.TryGetProperty("hash", out var h) ? h.GetString() ?? "" : "",
                                    Amount = (decimal)value / 1_000_000_000_000_000_000m,
                                    FromAddress = from ?? "",
                                    ToAddress = to ?? "",
                                    Confirmations = (int)(latestBlock - i) + 1,
                                    Status = "confirmed"
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Ethereum transactions for {Address}", address);
        }
        return transactions;
    }

    public async Task<BlockchainFeeEstimate> EstimateFeeAsync()
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendEthRpcRequestAsync(client, "eth_gasPrice", Array.Empty<object>());
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                var hexGas = result.GetString()?.Replace("0x", "") ?? "0";
                var gasWei = BigInteger.Parse(hexGas, System.Globalization.NumberStyles.HexNumber);
                var gasGwei = (decimal)gasWei / 1_000_000_000m;

                return new BlockchainFeeEstimate
                {
                    Low = gasGwei * 0.8m,
                    Medium = gasGwei,
                    High = gasGwei * 1.5m,
                    Unit = "Gwei"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating Ethereum fee");
        }

        return new BlockchainFeeEstimate
        {
            Low = 10,
            Medium = 20,
            High = 50,
            Unit = "Gwei"
        };
    }

    public async Task<string> SendTransactionAsync(string fromAddress, string toAddress, decimal amount, decimal fee)
    {
        try
        {
            using var client = CreateRpcClient();

            // Note: In production, you'd sign the transaction with the private key
            // This is a simplified version using personal_sendTransaction (requires unlocked account on node)
            var amountHex = "0x" + ((long)(amount * 1_000_000_000_000_000_000m)).ToString("x");

            var response = await SendEthRpcRequestAsync(client, "eth_sendTransaction", new object[]
            {
                new
                {
                    from = fromAddress,
                    to = toAddress,
                    value = amountHex,
                    gas = "0x5208", // 21000 gas
                    gasPrice = "0x" + ((long)(fee * 1_000_000_000m)).ToString("x")
                }
            });

            var txHash = response?.GetProperty("result").GetString() ?? throw new Exception("Failed to send transaction");
            _logger.LogInformation("Ethereum transaction sent: {TxHash}", txHash);
            return txHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Ethereum transaction");
            throw;
        }
    }

    public async Task<List<Utxo>> GetUtxosAsync(string address)
    {
        // Ethereum doesn't use UTXO model, return empty
        return new List<Utxo>();
    }

    public async Task<int> GetConfirmationsAsync(string txHash)
    {
        var tx = await GetTransactionAsync(txHash);
        return tx?.Confirmations ?? 0;
    }

    public async Task<bool> ValidateAddressAsync(string address)
    {
        return !string.IsNullOrEmpty(address) &&
               address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               address.Length == 42 &&
               System.Text.RegularExpressions.Regex.IsMatch(address, @"^0x[0-9a-fA-F]{40}$");
    }

    private HttpClient CreateRpcClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_rpcUrl);
        return client;
    }

    private async Task<JsonElement?> SendEthRpcRequestAsync(HttpClient client, string method, object[] parameters)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params = parameters
        };

        var response = await client.PostAsJsonAsync("/", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static byte[] Keccak256(byte[] data)
    {
        // Simplified Keccak256 - in production use SHA3.Net or similar
        // This uses SHA256 as a placeholder; for real Ethereum, use proper Keccak256
        return SHA256.HashData(data);
    }
}
