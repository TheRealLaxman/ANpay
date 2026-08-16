using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ANpay.Api.Services.Crypto;

public class BitcoinRpcService : IBlockchainService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BitcoinRpcService> _logger;

    public BitcoinRpcService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<BitcoinRpcService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<BlockchainWalletInfo> GenerateWalletAsync(string? label = null)
    {
        try
        {
            using var client = CreateRpcClient();

            // Generate a new address via RPC
            var response = await SendRpcRequestAsync(client, "getnewaddress", new object[] { label ?? "anpay", "bech32" });
            var address = response?.GetProperty("result").GetString() ?? throw new Exception("Failed to generate address");

            // Get the private key
            var privKeyResponse = await SendRpcRequestAsync(client, "dumpprivkey", new object[] { address });
            var privateKey = privKeyResponse?.GetProperty("result").GetString();

            return new BlockchainWalletInfo
            {
                Address = address,
                PrivateKey = privateKey,
                Network = "Bitcoin"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Bitcoin wallet");
            // Fallback to local generation
            return GenerateLocalWallet();
        }
    }

    public async Task<decimal> GetBalanceAsync(string address)
    {
        try
        {
            using var client = CreateRpcClient();

            // Use listunspent to calculate balance for address
            var response = await SendRpcRequestAsync(client, "listunspent", new object[] { 0, 9999999, new[] { address } });
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                var utxos = result.EnumerateArray();
                return utxos.Sum(u => u.GetProperty("amount").GetDecimal());
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Bitcoin balance for {Address}", address);
            return 0;
        }
    }

    public async Task<BlockchainTransaction?> GetTransactionAsync(string txHash)
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendRpcRequestAsync(client, "gettransaction", new object[] { txHash });
            if (response == null) return null;

            var result = response.Value.GetProperty("result");
            return new BlockchainTransaction
            {
                TxHash = txHash,
                Amount = result.GetProperty("amount").GetDecimal(),
                Fee = result.TryGetProperty("fee", out var fee) ? Math.Abs(fee.GetDecimal()) : 0,
                Confirmations = result.GetProperty("confirmations").GetInt32(),
                BlockTime = result.TryGetProperty("blocktime", out var bt) && bt.GetInt64() > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(bt.GetInt64()).UtcDateTime
                    : null,
                Status = result.GetProperty("confirmations").GetInt32() > 0 ? "confirmed" : "pending"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Bitcoin transaction {TxHash}", txHash);
            return null;
        }
    }

    public async Task<List<BlockchainTransaction>> GetTransactionsAsync(string address, int limit = 50)
    {
        var transactions = new List<BlockchainTransaction>();
        try
        {
            using var client = CreateRpcClient();

            // Get received by address
            var response = await SendRpcRequestAsync(client, "listtransactions", new object[] { "*", limit, 0, true });
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                foreach (var tx in result.EnumerateArray())
                {
                    var txAddress = tx.TryGetProperty("address", out var addr) ? addr.GetString() : "";
                    if (txAddress == address)
                    {
                        transactions.Add(new BlockchainTransaction
                        {
                            TxHash = tx.TryGetProperty("txid", out var txid) ? txid.GetString() ?? "" : "",
                            Amount = tx.TryGetProperty("amount", out var amt) ? amt.GetDecimal() : 0,
                            Fee = tx.TryGetProperty("fee", out var fee) ? Math.Abs(fee.GetDecimal()) : 0,
                            FromAddress = tx.TryGetProperty("category", out var cat) && cat.GetString() == "receive" ? "" : address,
                            ToAddress = tx.TryGetProperty("category", out var c) && c.GetString() == "receive" ? address : "",
                            Confirmations = tx.TryGetProperty("confirmations", out var conf) ? conf.GetInt32() : 0,
                            Status = tx.TryGetProperty("confirmations", out var co) && co.GetInt32() > 0 ? "confirmed" : "pending"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Bitcoin transactions for {Address}", address);
        }
        return transactions;
    }

    public async Task<BlockchainFeeEstimate> EstimateFeeAsync()
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendRpcRequestAsync(client, "estimatesmartfee", new object[] { 6 });
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                var feeRate = result.GetProperty("feerate").GetDecimal();
                return new BlockchainFeeEstimate
                {
                    Low = feeRate * 0.5m,
                    Medium = feeRate,
                    High = feeRate * 2,
                    Unit = "BTC/kB"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating Bitcoin fee");
        }

        return new BlockchainFeeEstimate
        {
            Low = 0.00001m,
            Medium = 0.00005m,
            High = 0.0001m,
            Unit = "BTC/kB"
        };
    }

    public async Task<string> SendTransactionAsync(string fromAddress, string toAddress, decimal amount, decimal fee)
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendRpcRequestAsync(client, "sendtoaddress", new object[] { toAddress, amount, "", "", false, true, (string?)null, (string?)null, (string?)null, fee });
            var txHash = response?.GetProperty("result").GetString() ?? throw new Exception("Failed to send transaction");
            _logger.LogInformation("Bitcoin transaction sent: {TxHash}", txHash);
            return txHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Bitcoin transaction");
            throw;
        }
    }

    public async Task<List<Utxo>> GetUtxosAsync(string address)
    {
        var utxos = new List<Utxo>();
        try
        {
            using var client = CreateRpcClient();
            var response = await SendRpcRequestAsync(client, "listunspent", new object[] { 1, 9999999, new[] { address } });
            if (response != null && response.Value.TryGetProperty("result", out var result))
            {
                foreach (var utxo in result.EnumerateArray())
                {
                    utxos.Add(new Utxo
                    {
                        TxHash = utxo.GetProperty("txid").GetString() ?? "",
                        Vout = utxo.GetProperty("vout").GetInt32(),
                        Amount = utxo.GetProperty("amount").GetDecimal(),
                        Address = utxo.TryGetProperty("address", out var addr) ? addr.GetString() ?? "" : address,
                        IsConfirmed = utxo.TryGetProperty("confirmations", out var conf) && conf.GetInt32() > 0
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting UTXOs for {Address}", address);
        }
        return utxos;
    }

    public async Task<int> GetConfirmationsAsync(string txHash)
    {
        var tx = await GetTransactionAsync(txHash);
        return tx?.Confirmations ?? 0;
    }

    public async Task<bool> ValidateAddressAsync(string address)
    {
        try
        {
            using var client = CreateRpcClient();
            var response = await SendRpcRequestAsync(client, "validateaddress", new object[] { address });
            return response?.GetProperty("result").GetProperty("isvalid").GetBoolean() ?? false;
        }
        catch
        {
            return false;
        }
    }

    private HttpClient CreateRpcClient()
    {
        var url = _configuration["Crypto:BitcoinRpcUrl"] ?? "http://localhost:8332";
        var user = _configuration["Crypto:BitcoinRpcUser"] ?? "";
        var pass = _configuration["Crypto:BitcoinRpcPassword"] ?? "";

        var client = _httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        client.BaseAddress = new Uri(url);
        return client;
    }

    private async Task<JsonElement?> SendRpcRequestAsync(HttpClient client, string method, object[] parameters)
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

    private static BlockchainWalletInfo GenerateLocalWallet()
    {
        using var rng = RandomNumberGenerator.Create();
        var privateKeyBytes = new byte[32];
        rng.GetBytes(privateKeyBytes);

        // SHA256 hash for address bytes
        var addressHash = SHA256.HashData(privateKeyBytes);
        // Prepend version byte 0x00 for mainnet P2PKH
        var versionedHash = new byte[addressHash.Length + 1];
        versionedHash[0] = 0x00;
        Array.Copy(addressHash, 0, versionedHash, 1, addressHash.Length);
        // Double SHA256 for checksum
        var checksum = SHA256.HashData(SHA256.HashData(versionedHash));
        // Take first 4 bytes of checksum
        var fullAddress = new byte[versionedHash.Length + 4];
        Array.Copy(versionedHash, fullAddress, versionedHash.Length);
        Array.Copy(checksum, 0, fullAddress, versionedHash.Length, 4);
        // Base58 encode
        var address = Base58Encode(fullAddress);

        return new BlockchainWalletInfo
        {
            Address = address,
            PrivateKey = Convert.ToBase64String(privateKeyBytes),
            Network = "Bitcoin (Local Generated - not production-ready, use RPC)"
        };
    }

    private static string Base58Encode(byte[] data)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            int carry = b;
            for (int j = 0; j < sb.Length; j++)
            {
                int x = (alphabet.IndexOf(sb[j]) << 8) + carry;
                sb[j] = alphabet[x % 58];
                carry = x / 58;
            }
            while (carry > 0)
            {
                sb.Append(alphabet[carry % 58]);
                carry /= 58;
            }
        }
        // Add leading 1s for leading zero bytes
        foreach (var b in data)
        {
            if (b == 0) sb.Append('1');
            else break;
        }
        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
}
