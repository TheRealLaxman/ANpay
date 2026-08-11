using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class CryptoService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CryptoService> _logger;

    public CryptoService(ApplicationDbContext context, ILogger<CryptoService> logger)
    {
        _context = context;
        _logger = logger;
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

        var address = GenerateAddress(asset, network);

        var wallet = new CryptoWallet
        {
            UserId = userId,
            Asset = asset,
            Network = network,
            Address = address
        };

        _context.CryptoWallets.Add(wallet);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto wallet created: {Asset} on {Network} for user {UserId}", asset, network, userId);
        return wallet;
    }

    public async Task<CryptoTransaction> DepositAsync(string userId, Guid walletId, decimal amount, string txHash)
    {
        var wallet = await _context.CryptoWallets
            .FirstOrDefaultAsync(cw => cw.Id == walletId && cw.UserId == userId)
            ?? throw new NotFoundException("Crypto wallet not found");

        var networkConfig = await GetNetworkConfigAsync(wallet.Network);

        var tx = new CryptoTransaction
        {
            CryptoWalletId = walletId,
            Type = CryptoTransactionType.Deposit,
            Asset = wallet.Asset,
            Network = wallet.Network,
            Amount = amount,
            TxHash = txHash,
            ToAddress = wallet.Address,
            RequiredConfirmations = networkConfig?.DefaultConfirmations ?? 12,
            Status = CryptoTransactionStatus.Pending
        };

        _context.CryptoTransactions.Add(tx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto deposit initiated: {Amount} {Asset} on {Network}", amount, wallet.Asset, wallet.Network);
        return tx;
    }

    public async Task<CryptoTransaction> WithdrawAsync(string userId, Guid walletId, decimal amount, string toAddress)
    {
        var wallet = await _context.CryptoWallets
            .FirstOrDefaultAsync(cw => cw.Id == walletId && cw.UserId == userId)
            ?? throw new NotFoundException("Crypto wallet not found");

        var networkConfig = await GetNetworkConfigAsync(wallet.Network);
        var fee = networkConfig?.AverageTxFee ?? 0.001m;

        var tx = new CryptoTransaction
        {
            CryptoWalletId = walletId,
            Type = CryptoTransactionType.Withdrawal,
            Asset = wallet.Asset,
            Network = wallet.Network,
            Amount = amount,
            NetworkFee = fee,
            FromAddress = wallet.Address,
            ToAddress = toAddress,
            Status = CryptoTransactionStatus.Pending
        };

        _context.CryptoTransactions.Add(tx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Crypto withdrawal initiated: {Amount} {Asset} to {Address}", amount, wallet.Asset, toAddress);
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

    private string GenerateAddress(CryptoAsset asset, CryptoNetwork network)
    {
        var random = new Random();
        var bytes = new byte[20];
        random.NextBytes(bytes);

        return network switch
        {
            CryptoNetwork.Ethereum or CryptoNetwork.BnbSmartChain => "0x" + Convert.ToHexString(bytes).ToLower(),
            CryptoNetwork.Bitcoin => "1" + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Substring(0, 33),
            CryptoNetwork.Tron => "T" + Convert.ToHexString(bytes).ToUpper().Substring(0, 33),
            CryptoNetwork.Solana => Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Substring(0, 44),
            _ => Convert.ToHexString(bytes)
        };
    }

    private async Task<CryptoNetworkConfig?> GetNetworkConfigAsync(CryptoNetwork network)
    {
        return await _context.CryptoNetworkConfigs.FirstOrDefaultAsync(c => c.Network == network && c.IsActive);
    }
}
