using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace ANpay.Api.Services;

public class QrPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QrPaymentService> _logger;
    private readonly IConfiguration _configuration;

    public QrPaymentService(ApplicationDbContext context, ILogger<QrPaymentService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<QrCode> GenerateQrCodeAsync(string userId, QrCodeType type, decimal? fixedAmount = null,
        Guid? walletId = null, Guid? merchantId = null, string description = "", int usageLimit = 1, int expiresInMinutes = 30)
    {
        var code = GenerateCode();
        var payload = CreatePayload(type, userId, fixedAmount, walletId, merchantId);

        var qrCode = new QrCode
        {
            CreatedById = userId,
            Type = type,
            Code = code,
            Payload = payload,
            FixedAmount = fixedAmount,
            WalletId = walletId,
            MerchantId = merchantId,
            Description = description,
            UsageLimit = usageLimit,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        };

        _context.QrCodes.Add(qrCode);
        await _context.SaveChangesAsync();

        _logger.LogInformation("QR code generated: {Code} by {UserId}", code, userId);
        return qrCode;
    }

    public async Task<QrCodeScanResult> ScanQrCodeAsync(string code, string scannerUserId)
    {
        var qr = await _context.QrCodes
            .FirstOrDefaultAsync(q => q.Code == code)
            ?? throw new NotFoundException("QR code not found");

        if (qr.Status != QrCodeStatus.Active)
            throw new ValidationException("QR code is no longer active");

        if (qr.ExpiresAt.HasValue && qr.ExpiresAt < DateTime.UtcNow)
        {
            qr.Status = QrCodeStatus.Expired;
            await _context.SaveChangesAsync();
            throw new ValidationException("QR code has expired");
        }

        if (qr.UsageCount >= qr.UsageLimit)
        {
            qr.Status = QrCodeStatus.Used;
            await _context.SaveChangesAsync();
            throw new ValidationException("QR code usage limit reached");
        }

        var creator = await _context.Users.FindAsync(qr.CreatedById);

        return new QrCodeScanResult
        {
            QrCodeId = qr.Id,
            Type = qr.Type,
            Amount = qr.FixedAmount,
            Description = qr.Description,
            CreatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "Unknown",
            WalletId = qr.WalletId,
            MerchantId = qr.MerchantId,
            IsMerchant = qr.Type == QrCodeType.MerchantPayment
        };
    }

    public async Task ProcessQrPaymentAsync(Guid qrCodeId, string payerUserId, decimal amount, Guid? sourceWalletId)
    {
        var qr = await _context.QrCodes.FindAsync(qrCodeId)
            ?? throw new NotFoundException("QR code not found");

        if (qr.FixedAmount.HasValue && qr.FixedAmount.Value != amount)
            throw new ValidationException($"QR code requires a fixed amount of {qr.FixedAmount.Value}");

        var sourceWallet = sourceWalletId.HasValue
            ? await _context.Wallets.FirstOrDefaultAsync(w => w.Id == sourceWalletId.Value && w.UserId == payerUserId)
            : await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == payerUserId && w.IsActive);

        if (sourceWallet == null)
            throw new NotFoundException("Source wallet not found");

        if (sourceWallet.AvailableBalance < amount)
            throw new ValidationException("Insufficient available balance");

        var destinationWallet = qr.WalletId.HasValue
            ? await _context.Wallets.FindAsync(qr.WalletId.Value)
            : await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == qr.CreatedById && w.IsActive);

        if (destinationWallet == null)
            throw new NotFoundException("Destination wallet not found");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var sourceBalanceBefore = sourceWallet.Balance;
            var destBalanceBefore = destinationWallet.Balance;

            sourceWallet.Balance -= amount;
            destinationWallet.Balance += amount;

            var debitTx = new Transaction
            {
                WalletId = sourceWallet.Id,
                Type = TransactionType.Payment,
                Amount = amount,
                BalanceBefore = sourceBalanceBefore,
                BalanceAfter = sourceWallet.Balance,
                Description = $"QR payment to {qr.Description}",
                ReferenceNumber = $"QR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = TransactionStatus.Completed
            };

            var creditTx = new Transaction
            {
                WalletId = destinationWallet.Id,
                Type = TransactionType.Deposit,
                Amount = amount,
                BalanceBefore = destBalanceBefore,
                BalanceAfter = destinationWallet.Balance,
                Description = $"QR payment from user",
                ReferenceNumber = debitTx.ReferenceNumber,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(debitTx);
            _context.Transactions.Add(creditTx);

            qr.UsageCount++;
            if (qr.UsageCount >= qr.UsageLimit)
                qr.Status = QrCodeStatus.Used;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("QR payment processed: {QrCodeId} by {UserId} amount {Amount}", qrCodeId, payerUserId, amount);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<QrCode>> GetUserQrCodesAsync(string userId)
    {
        return await _context.QrCodes
            .Where(q => q.CreatedById == userId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<PaymentLink> CreatePaymentLinkAsync(string userId, string title, string description,
        decimal? fixedAmount = null, string currency = "NGN", Guid? merchantId = null, int expiresInDays = 7)
    {
        var linkId = Guid.NewGuid().ToString("N").Substring(0, 12);
        var linkUrl = $"https://anpay.com/pay/{linkId}";

        var link = new PaymentLink
        {
            CreatedById = userId,
            MerchantId = merchantId,
            Title = title,
            Description = description,
            FixedAmount = fixedAmount,
            Currency = currency,
            LinkUrl = linkUrl,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays)
        };

        _context.PaymentLinks.Add(link);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment link created: {LinkUrl} by {UserId}", linkUrl, userId);
        return link;
    }

    public async Task<PaymentLink?> GetPaymentLinkAsync(string linkId)
    {
        return await _context.PaymentLinks
            .FirstOrDefaultAsync(pl => pl.LinkUrl.Contains(linkId) && pl.IsActive);
    }

    private string GenerateCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 20);
    }

    private string CreatePayload(QrCodeType type, string userId, decimal? amount, Guid? walletId, Guid? merchantId)
    {
        var payload = $"{type}:{userId}:{amount}:{walletId}:{merchantId}:{DateTime.UtcNow:yyyyMMddHHmmss}";
        var secret = _configuration["QrPayment:HmacSecret"] ?? _configuration["JwtSettings:Secret"] ?? "";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToBase64String(hash)[..16];
        return $"{payload}:{signature}";
    }
}

public class QrCodeScanResult
{
    public Guid QrCodeId { get; set; }
    public QrCodeType Type { get; set; }
    public decimal? Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public Guid? WalletId { get; set; }
    public Guid? MerchantId { get; set; }
    public bool IsMerchant { get; set; }
}
