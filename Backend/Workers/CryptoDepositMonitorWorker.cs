using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Services.Crypto;

namespace ANpay.Api.Workers;

public class CryptoDepositMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CryptoDepositMonitorWorker> _logger;

    public CryptoDepositMonitorWorker(IServiceProvider serviceProvider, ILogger<CryptoDepositMonitorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CryptoDepositMonitorWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Check pending crypto transactions
                await CheckPendingDepositsAsync(context, stoppingToken);

                // Check pending withdrawals
                await ProcessPendingWithdrawalsAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in crypto deposit monitoring");
            }

            // Run every 2 minutes
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }

        _logger.LogInformation("CryptoDepositMonitorWorker stopped");
    }

    private async Task CheckPendingDepositsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var pendingDeposits = await context.CryptoTransactions
            .Include(ct => ct.CryptoWallet)
            .Where(ct => ct.Type == CryptoTransactionType.Deposit
                        && (ct.Status == CryptoTransactionStatus.Pending || ct.Status == CryptoTransactionStatus.Confirming))
            .ToListAsync(ct);

        foreach (var tx in pendingDeposits)
        {
            if (tx.CryptoWallet == null) continue;

            try
            {
                var blockchainService = GetBlockchainService(tx.Network);
                var onChainTx = await blockchainService.GetTransactionAsync(tx.TxHash!);

                if (onChainTx != null)
                {
                    tx.Confirmations = onChainTx.Confirmations;

                    if (onChainTx.Confirmations >= tx.RequiredConfirmations)
                    {
                        tx.Status = CryptoTransactionStatus.Completed;
                        tx.CompletedAt = DateTime.UtcNow;

                        _logger.LogInformation("Crypto deposit confirmed: {Amount} {Asset}. TxHash: {TxHash}. Confirmations: {Confirmations}",
                            tx.Amount, tx.Asset, tx.TxHash, tx.Confirmations);
                    }
                    else if (onChainTx.Confirmations > 0)
                    {
                        tx.Status = CryptoTransactionStatus.Confirming;
                        _logger.LogDebug("Crypto deposit confirming: {Amount} {Asset}. Confirmations: {Confirmations}/{Required}",
                            tx.Amount, tx.Asset, tx.Confirmations, tx.RequiredConfirmations);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking deposit status for {TxHash}", tx.TxHash);
            }
        }

        if (pendingDeposits.Any())
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task ProcessPendingWithdrawalsAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var pendingWithdrawals = await context.CryptoTransactions
            .Include(ct => ct.CryptoWallet)
            .Where(ct => ct.Type == CryptoTransactionType.Withdrawal
                        && ct.Status == CryptoTransactionStatus.Pending)
            .ToListAsync(ct);

        foreach (var tx in pendingWithdrawals)
        {
            if (tx.CryptoWallet == null) continue;

            try
            {
                var blockchainService = GetBlockchainService(tx.Network);

                // In production, this would sign and broadcast the transaction
                // For now, we simulate processing
                _logger.LogInformation("Processing withdrawal: {Amount} {Asset} to {ToAddress}",
                    tx.Amount, tx.Asset, tx.ToAddress);

                // Simulate transaction broadcast (in production, sign with private key and broadcast)
                tx.Status = CryptoTransactionStatus.Confirming;
                tx.TxHash = $"SIMULATED-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";
                tx.BlockExplorerUrl = GetBlockExplorerUrl(tx.Network, tx.TxHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal {TxId}", tx.Id);
                tx.Status = CryptoTransactionStatus.Failed;
            }
        }

        if (pendingWithdrawals.Any())
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private IBlockchainService GetBlockchainService(CryptoNetwork network)
    {
        using var scope = _serviceProvider.CreateScope();
        return network switch
        {
            CryptoNetwork.Bitcoin => scope.ServiceProvider.GetRequiredService<BitcoinRpcService>(),
            CryptoNetwork.Ethereum or CryptoNetwork.BnbSmartChain => scope.ServiceProvider.GetRequiredService<EthereumRpcService>(),
            _ => throw new NotSupportedException($"Blockchain not supported: {network}")
        };
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

        return !string.IsNullOrEmpty(txHash) ? $"{baseUrl}/tx/{txHash}" : baseUrl;
    }
}
