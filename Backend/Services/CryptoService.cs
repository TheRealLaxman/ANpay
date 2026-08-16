using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;
using ANpay.Api.Services.Crypto;

namespace ANpay.Api.Services;

public class CryptoService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CryptoService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public CryptoService(
        ApplicationDbContext context,
        ILogger<CryptoService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private IBlockchainService GetBlockchainService(CryptoNetwork network)
    {
        return network switch
        {
            CryptoNetwork.Bitcoin => _serviceProvider.GetRequiredService<BitcoinRpcService>(),
            CryptoNetwork.Ethereum or CryptoNetwork.BnbSmartChain => _serviceProvider.GetRequiredService<EthereumRpcService>(),
            _ => throw new ValidationException($"Blockchain not supported for {network}")
        };
    }

    public async Task<List<CryptoWallet>> GetUserWalletsAsync(string userId)
    {
        return await _context.CryptoWallets
            .Include(cw => cw.Transactions)
            .Where(cw => cw.UserId == userId && cw.IsActive)
            .ToListAsync();
    }

    public async Task<CryptoWallet> CreateWalletAsync(string userId, CryptoAsset asset, CryptoNetwork network)
    {
        if (await _context.CryptoWallets.AnyAsync(cw => cw.UserId == userId && cw.Asset == asset && cw.Network == network && cw.IsActive))
            throw new ValidationException("Crypto wallet already exists for this asset and network");

        var blockchainService = GetBlockchainService(network);
        var walletInfo = await blockchainService.GenerateWalletAsync($"{asset}-{network}");

        var wallet = new CryptoWallet
        {
            UserId = userId,
            Asset = asset,
            Network = network,
            Address = walletInfo.Address,
            DerivationPath = "m/44'/0'/0'/0/0"
        };

        _context.CryptoWallets.Add(wallet);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto wallet created: {Asset} on {Network} for user {UserId}. Address: {Address}",
            asset, network, userId, walletInfo.Address);

        return wallet;
    }

    public async Task<CryptoTransaction> DepositAsync(string userId, Guid walletId, decimal amount, string txHash)
    {
        var wallet = await _context.CryptoWallets
            .FirstOrDefaultAsync(cw => cw.Id == walletId && cw.UserId == userId)
            ?? throw new NotFoundException("Crypto wallet not found");

        var networkConfig = await GetNetworkConfigAsync(wallet.Network);

        // Verify the transaction on-chain
        var blockchainService = GetBlockchainService(wallet.Network);
        var onChainTx = await blockchainService.GetTransactionAsync(txHash);

        int confirmations = 0;
        string? blockExplorerUrl = null;

        if (onChainTx != null)
        {
            confirmations = onChainTx.Confirmations;
            blockExplorerUrl = GetBlockExplorerUrl(wallet.Network, txHash);

            // Verify the amount matches
            if (onChainTx.Amount < amount)
            {
                throw new ValidationException($"Transaction amount ({onChainTx.Amount}) is less than declared ({amount})");
            }

            // Verify the recipient address matches
            if (!string.Equals(onChainTx.ToAddress, wallet.Address, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Transaction recipient does not match wallet address");
            }
        }

        var requiredConfirmations = networkConfig?.DefaultConfirmations ?? 12;
        var status = confirmations >= requiredConfirmations
            ? CryptoTransactionStatus.Completed
            : confirmations > 0
                ? CryptoTransactionStatus.Confirming
                : CryptoTransactionStatus.Pending;

        var tx = new CryptoTransaction
        {
            CryptoWalletId = walletId,
            Type = CryptoTransactionType.Deposit,
            Asset = wallet.Asset,
            Network = wallet.Network,
            Amount = amount,
            TxHash = txHash,
            ToAddress = wallet.Address,
            FromAddress = onChainTx?.FromAddress,
            Confirmations = confirmations,
            RequiredConfirmations = requiredConfirmations,
            Status = status,
            BlockExplorerUrl = blockExplorerUrl
        };

        if (status == CryptoTransactionStatus.Completed)
        {
            tx.CompletedAt = DateTime.UtcNow;
        }

        _context.CryptoTransactions.Add(tx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto deposit recorded: {Amount} {Asset} on {Network}. Status: {Status}. Confirmations: {Confirmations}/{Required}",
            amount, wallet.Asset, wallet.Network, status, confirmations, requiredConfirmations);

        return tx;
    }

    public async Task<CryptoTransaction> WithdrawAsync(string userId, Guid walletId, decimal amount, string toAddress)
    {
        var wallet = await _context.CryptoWallets
            .FirstOrDefaultAsync(cw => cw.Id == walletId && cw.UserId == userId)
            ?? throw new NotFoundException("Crypto wallet not found");

        var blockchainService = GetBlockchainService(wallet.Network);

        // Validate the destination address
        if (!await blockchainService.ValidateAddressAsync(toAddress))
        {
            throw new ValidationException("Invalid destination address");
        }

        // Check balance
        var balance = await blockchainService.GetBalanceAsync(wallet.Address);
        var feeEstimate = await blockchainService.EstimateFeeAsync();
        var estimatedFee = feeEstimate.Medium;

        if (balance < amount + estimatedFee)
        {
            throw new ValidationException($"Insufficient balance. Available: {balance}, Required: {amount + estimatedFee}");
        }

        var tx = new CryptoTransaction
        {
            CryptoWalletId = walletId,
            Type = CryptoTransactionType.Withdrawal,
            Asset = wallet.Asset,
            Network = wallet.Network,
            Amount = amount,
            NetworkFee = estimatedFee,
            FromAddress = wallet.Address,
            ToAddress = toAddress,
            Status = CryptoTransactionStatus.Pending,
            BlockExplorerUrl = GetBlockExplorerUrl(wallet.Network, null)
        };

        _context.CryptoTransactions.Add(tx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto withdrawal initiated: {Amount} {Asset} to {Address}. Fee: {Fee}",
            amount, wallet.Asset, toAddress, estimatedFee);

        return tx;
    }

    public async Task<List<CryptoTransaction>> GetTransactionsAsync(string userId, Guid? walletId = null)
    {
        var query = _context.CryptoTransactions
            .Include(ct => ct.CryptoWallet)
            .Where(ct => ct.CryptoWallet.UserId == userId);

        if (walletId.HasValue)
            query = query.Where(ct => ct.CryptoWalletId == walletId.Value);

        return await query.OrderByDescending(ct => ct.CreatedAt).ToListAsync();
    }

    public async Task<List<CryptoNetworkConfig>> GetNetworkConfigsAsync()
    {
        return await _context.CryptoNetworkConfigs.Where(c => c.IsActive).ToListAsync();
    }

    public async Task<CryptoNetworkConfig> UpsertNetworkConfigAsync(CryptoNetworkConfig config)
    {
        var existing = await _context.CryptoNetworkConfigs.FirstOrDefaultAsync(c => c.Network == config.Network);
        if (existing == null)
        {
            _context.CryptoNetworkConfigs.Add(config);
        }
        else
        {
            existing.Name = config.Name;
            existing.Symbol = config.Symbol;
            existing.ChainId = config.ChainId;
            existing.GasPriceGwei = config.GasPriceGwei;
            existing.AverageTxFee = config.AverageTxFee;
            existing.AverageBlockTimeSeconds = config.AverageBlockTimeSeconds;
            existing.DefaultConfirmations = config.DefaultConfirmations;
            existing.ExplorerBaseUrl = config.ExplorerBaseUrl;
        }
        await _context.SaveChangesAsync();
        return config;
    }

    public async Task<BlockchainFeeEstimate> GetFeeEstimateAsync(CryptoNetwork network)
    {
        var blockchainService = GetBlockchainService(network);
        return await blockchainService.EstimateFeeAsync();
    }

    public async Task<decimal> GetOnChainBalanceAsync(Guid walletId, string userId)
    {
        var wallet = await _context.CryptoWallets
            .FirstOrDefaultAsync(cw => cw.Id == walletId && cw.UserId == userId)
            ?? throw new NotFoundException("Crypto wallet not found");

        var blockchainService = GetBlockchainService(wallet.Network);
        return await blockchainService.GetBalanceAsync(wallet.Address);
    }

    public async Task<CryptoTransaction?> RefreshTransactionStatusAsync(Guid transactionId, string userId)
    {
        var tx = await _context.CryptoTransactions
            .Include(ct => ct.CryptoWallet)
            .FirstOrDefaultAsync(ct => ct.Id == transactionId && ct.CryptoWallet.UserId == userId);

        if (tx == null) return null;

        if (tx.Status == CryptoTransactionStatus.Pending || tx.Status == CryptoTransactionStatus.Confirming)
        {
            var blockchainService = GetBlockchainService(tx.Network);

            if (!string.IsNullOrEmpty(tx.TxHash))
            {
                var onChainTx = await blockchainService.GetTransactionAsync(tx.TxHash);
                if (onChainTx != null)
                {
                    tx.Confirmations = onChainTx.Confirmations;

                    if (onChainTx.Confirmations >= tx.RequiredConfirmations)
                    {
                        tx.Status = CryptoTransactionStatus.Completed;
                        tx.CompletedAt = DateTime.UtcNow;
                    }
                    else if (onChainTx.Confirmations > 0)
                    {
                        tx.Status = CryptoTransactionStatus.Confirming;
                    }

                    await _context.SaveChangesAsync();
                }
            }
        }

        return tx;
    }

    private async Task<CryptoNetworkConfig?> GetNetworkConfigAsync(CryptoNetwork network)
    {
        return await _context.CryptoNetworkConfigs.FirstOrDefaultAsync(c => c.Network == network && c.IsActive);
    }

    private static string GetBlockExplorerUrl(CryptoNetwork network, string? txHash)
    {
        var baseUrl = network switch
        {
            CryptoNetwork.Bitcoin => "https://blockstream.info",
            CryptoNetwork.Ethereum => "https://etherscan.io",
            CryptoNetwork.BnbSmartChain => "https://bscscan.com",
            CryptoNetwork.Tron => "https://tronscan.org",
            CryptoNetwork.Solana => "https://solscan.io",
            _ => ""
        };

        if (!string.IsNullOrEmpty(txHash))
        {
            return $"{baseUrl}/tx/{txHash}";
        }
        return baseUrl;
    }
}
