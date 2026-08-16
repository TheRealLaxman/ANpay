using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class PosService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PosService> _logger;

    public PosService(ApplicationDbContext context, ILogger<PosService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PosDevice> RegisterDeviceAsync(Guid merchantId, string deviceSerial, string deviceModel, bool supportsNfc = true, bool supportsChip = true, bool supportsSwipe = true, bool supportsTapToPay = false)
    {
        var device = new PosDevice
        {
            MerchantId = merchantId,
            DeviceSerial = deviceSerial,
            DeviceModel = deviceModel,
            SupportsNfc = supportsNfc,
            SupportsChip = supportsChip,
            SupportsSwipe = supportsSwipe,
            SupportsTapToPay = supportsTapToPay,
            Status = PosDeviceStatus.Inactive
        };

        _context.PosDevices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<List<PosDevice>> GetMerchantDevicesAsync(Guid merchantId)
    {
        return await _context.PosDevices.Where(pd => pd.MerchantId == merchantId).ToListAsync();
    }

    public async Task<PosDevice> ActivateDeviceAsync(Guid deviceId, Guid merchantId)
    {
        var device = await _context.PosDevices.FirstOrDefaultAsync(pd => pd.Id == deviceId && pd.MerchantId == merchantId);
        if (device == null) throw new NotFoundException("POS device not found");
        device.Status = PosDeviceStatus.Active;
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<PosTransaction> ProcessSaleAsync(Guid deviceId, Guid? walletId, decimal amount, string? description = null, bool isTapToPay = false, string? cardLast4 = null, string? cardType = null)
    {
        var device = await _context.PosDevices.FirstOrDefaultAsync(pd => pd.Id == deviceId);
        if (device == null) throw new NotFoundException("POS device not found");
        if (device.Status != PosDeviceStatus.Active) throw new ValidationException("POS device is not active");

        var merchant = await _context.Merchants.FindAsync(device.MerchantId);
        if (merchant == null) throw new NotFoundException("Merchant not found");

        var fee = amount * 1.5m / 100; // 1.5% fee
        var totalAmount = amount + fee;

        if (walletId.HasValue)
        {
            var wallet = await _context.Wallets.FindAsync(walletId.Value);
            if (wallet == null) throw new NotFoundException("Wallet not found");
            if (wallet.Balance < totalAmount) throw new ValidationException("Insufficient balance");

            wallet.Balance -= totalAmount;

            var txRecord = new Transaction
            {
                WalletId = walletId.Value,
                Type = TransactionType.Payment,
                Amount = amount,
                BalanceBefore = wallet.Balance + totalAmount,
                BalanceAfter = wallet.Balance,
                Description = description ?? $"POS payment at {merchant.BusinessName}",
                ReferenceNumber = $"POS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Fee = fee,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
        }

        var posTransaction = new PosTransaction
        {
            PosDeviceId = deviceId,
            WalletId = walletId,
            Type = PosTransactionType.Sale,
            Amount = amount,
            Fee = fee,
            Status = PosTransactionStatus.Completed,
            Description = description,
            ReferenceNumber = $"POS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            IsTapToPay = isTapToPay,
            CardLast4 = cardLast4,
            CardType = cardType,
            CompletedAt = DateTime.UtcNow
        };

        _context.PosTransactions.Add(posTransaction);
        device.LastSyncAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return posTransaction;
    }

    public async Task<List<PosTransaction>> GetDeviceTransactionsAsync(Guid deviceId, int page = 1, int pageSize = 20)
    {
        return await _context.PosTransactions
            .Where(pt => pt.PosDeviceId == deviceId)
            .OrderByDescending(pt => pt.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<PosTransaction> RefundTransactionAsync(Guid posTransactionId, string? reason = null)
    {
        var posTx = await _context.PosTransactions.FirstOrDefaultAsync(pt => pt.Id == posTransactionId);
        if (posTx == null) throw new NotFoundException("POS transaction not found");
        if (posTx.Status != PosTransactionStatus.Completed) throw new ValidationException("Transaction cannot be refunded");

        posTx.Status = PosTransactionStatus.Voided;

        if (posTx.WalletId.HasValue)
        {
            var wallet = await _context.Wallets.FindAsync(posTx.WalletId.Value);
            if (wallet != null)
            {
                wallet.Balance += posTx.Amount;

                var refundTx = new Transaction
                {
                    WalletId = posTx.WalletId.Value,
                    Type = TransactionType.Refund,
                    Amount = posTx.Amount,
                    BalanceBefore = wallet.Balance - posTx.Amount,
                    BalanceAfter = wallet.Balance,
                    Description = $"POS refund - {reason ?? "No reason provided"}",
                    ReferenceNumber = $"POS-REF-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    Status = TransactionStatus.Completed
                };

                _context.Transactions.Add(refundTx);
            }
        }

        await _context.SaveChangesAsync();
        return posTx;
    }
}
