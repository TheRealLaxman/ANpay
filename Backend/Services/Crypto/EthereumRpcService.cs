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
            // secp256k1 is the elliptic curve used by Ethereum
            var secp256k1Oid = "1.3.132.0.10";
            ECCurve curve;
            try
            {
                curve = ECCurve.CreateFromValue(secp256k1Oid);
            }
            catch (PlatformNotSupportedException)
            {
                _logger.LogWarning("secp256k1 not supported on this platform; wallet addresses may not be Ethereum-compatible");
                throw new PlatformNotSupportedException("Ethereum wallet generation requires secp256k1 support (available on Windows CNG and OpenSSL-backed systems)");
            }

            using var ecdsa = ECDsa.Create(curve);
            var privateKey = ecdsa.ExportECPrivateKey();

            // Derive uncompressed public key (64 bytes, no prefix byte)
            var subjectPublicKeyInfo = ecdsa.ExportSubjectPublicKeyInfo();
            // SubjectPublicKeyInfo for secp256k1 has a ~12-byte header; raw 64-byte key follows
            var publicKeyBytes = new byte[64];
            if (subjectPublicKeyInfo.Length >= 76)
            {
                Array.Copy(subjectPublicKeyInfo, subjectPublicKeyInfo.Length - 64, publicKeyBytes, 0, 64);
            }
            else if (subjectPublicKeyInfo.Length >= 64)
            {
                Array.Copy(subjectPublicKeyInfo, subjectPublicKeyInfo.Length - 64, publicKeyBytes, 0, 64);
            }

            // Hash the uncompressed public key with Keccak-256 for Ethereum address
            var hash = Keccak256(publicKeyBytes);
            var address = "0x" + Convert.ToHexString(hash[^20..]).ToLower();

            return new BlockchainWalletInfo
            {
                Address = address,
                PublicKey = Convert.ToHexString(subjectPublicKeyInfo),
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
            var weiAmount = (BigInteger)(amount * 1_000_000_000_000_000_000m);
            var amountHex = "0x" + weiAmount.ToString("x");

            var weiFee = (BigInteger)(fee * 1_000_000_000m);
            var feeHex = "0x" + weiFee.ToString("x");

            var response = await SendEthRpcRequestAsync(client, "eth_sendTransaction", new object[]
            {
                new
                {
                    from = fromAddress,
                    to = toAddress,
                    value = amountHex,
                    gas = "0x5208", // 21000 gas
                    gasPrice = feeHex
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

    public Task<bool> ValidateAddressAsync(string address)
    {
        var isValid = !string.IsNullOrEmpty(address) &&
               address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               address.Length == 42 &&
               System.Text.RegularExpressions.Regex.IsMatch(address, @"^0x[0-9a-fA-F]{40}$");
        return Task.FromResult(isValid);
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
        // Keccak-256 used by Ethereum (NOT SHA3-256 which has different padding)
        const int keccakRounds = 24;
        const int keccakStateSize = 25;

        ulong[] state = new ulong[keccakStateSize];

        // Keccak block size is 200 bytes (1600 bits)
        int blockSize = 200;
        // Rate is 1088 bits (136 bytes) for Keccak-256
        int rate = 136;

        // Pad input: append 0x01 byte, then 0x80 byte at end of block
        int inputLen = data.Length;
        int fullBlocks = inputLen / rate;
        int paddedLen = (fullBlocks + 1) * blockSize;
        var padded = new byte[paddedLen];
        Array.Copy(data, padded, inputLen);
        padded[inputLen] = 0x01;
        padded[paddedLen - 1] |= 0x80;

        // Process each 200-byte block
        for (int offset = 0; offset < paddedLen; offset += blockSize)
        {
            // XOR block into state (only first `rate` bytes map to state lanes)
            for (int i = 0; i < rate / 8; i++)
            {
                state[i] ^= BitConverter.ToUInt64(padded, offset + i * 8);
            }

            // Keccak-f[1600] permutation
            for (int round = 0; round < keccakRounds; round++)
            {
                // Theta
                ulong[] C = new ulong[5];
                for (int x = 0; x < 5; x++)
                    C[x] = state[x] ^ state[x + 5] ^ state[x + 10] ^ state[x + 15] ^ state[x + 20];

                for (int x = 0; x < 5; x++)
                {
                    ulong D = C[(x + 4) % 5] ^ BitRotateLeft(C[(x + 1) % 5], 1);
                    for (int y = 0; y < 25; y += 5)
                        state[y + x] ^= D;
                }

                // Rho and Pi — standard Keccak rotation offsets
                int[] rhoOffsets = { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14, 27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44 };
                int[] piMap = { 10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4, 15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1 };

                var tempState = new ulong[25];
                for (int i = 0; i < 25; i++)
                {
                    tempState[piMap[i]] = BitRotateLeft(state[i], rhoOffsets[i]);
                }
                Array.Copy(tempState, state, 25);

                // Chi
                for (int y = 0; y < 25; y += 5)
                {
                    ulong[] row = new ulong[5];
                    for (int x = 0; x < 5; x++)
                        row[x] = state[y + x];

                    for (int x = 0; x < 5; x++)
                        state[y + x] = row[x] ^ (~row[(x + 1) % 5] & row[(x + 2) % 5]);
                }

                // Iota
                ulong[] RC = { 0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000,
                    0x000000000000808B, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
                    0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
                    0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003,
                    0x8000000000008002, 0x8000000000000080, 0x000000000000800A, 0x800000008000000A,
                    0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008 };
                state[0] ^= RC[round];
            }
        }

        // Extract first 32 bytes (256 bits) of the state
        var result = new byte[32];
        for (int i = 0; i < 4; i++)
        {
            var bytes = BitConverter.GetBytes(state[i]);
            Array.Copy(bytes, 0, result, i * 8, 8);
        }
        return result;
    }

    private static ulong BitRotateLeft(ulong value, int offset)
    {
        return (value << offset) | (value >> (64 - offset));
    }
}
