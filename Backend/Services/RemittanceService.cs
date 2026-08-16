using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class RemittanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RemittanceService> _logger;
    private readonly ExchangeService _exchangeService;

    public RemittanceService(ApplicationDbContext context, ILogger<RemittanceService> logger, ExchangeService exchangeService)
    {
        _context = context;
        _logger = logger;
        _exchangeService = exchangeService;
    }

    public async Task<List<RemittancePartner>> GetPartnersAsync(string? country = null)
    {
        var query = _context.RemittancePartners.Where(rp => rp.IsActive);
        if (!string.IsNullOrEmpty(country))
            query = query.Where(rp => rp.Country == country);
        return await query.OrderBy(rp => rp.Name).ToListAsync();
    }

    public async Task<Remittance> SendMoneyAsync(string userId, Guid walletId, string recipientName, string recipientCountry, string recipientBankCode, string recipientAccountNumber, string recipientCurrency, decimal sendAmount, string? recipientBankName = null, RemittancePurpose purpose = RemittancePurpose.FamilySupport, string? purposeDescription = null)
    {
        _logger.LogInformation("Processing remittance from user {UserId} to {RecipientCountry}", userId, recipientCountry);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        // Get exchange rate
        var exchangeRate = await _exchangeService.GetRateAsync(wallet.Currency, recipientCurrency);
        if (exchangeRate == null) throw new ValidationException("Exchange rate not available for this currency pair");

        var receiveAmount = sendAmount * exchangeRate.BuyRate;
        var fee = CalculateFee(sendAmount);
        var totalDebit = sendAmount + fee;

        if (wallet.Balance < totalDebit)
            throw new ValidationException("Insufficient balance");

        // Check daily limit
        var todayTotal = await _context.Remittances
            .Where(r => r.SenderUserId == userId && r.CreatedAt.Date == DateTime.UtcNow.Date && r.Status != RemittanceStatus.Cancelled)
            .SumAsync(r => r.SendAmount);

        if (todayTotal + sendAmount > 50000)
            throw new ValidationException("Daily remittance limit exceeded (₦50,000)");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.Balance;
            wallet.Balance -= totalDebit;

            var remittance = new Remittance
            {
                SenderUserId = userId,
                SenderWalletId = walletId,
                RecipientName = recipientName,
                RecipientCountry = recipientCountry,
                RecipientBankCode = recipientBankCode,
                RecipientAccountNumber = recipientAccountNumber,
                RecipientCurrency = recipientCurrency,
                RecipientBankName = recipientBankName,
                SendAmount = sendAmount,
                SendCurrency = wallet.Currency,
                ReceiveAmount = receiveAmount,
                ExchangeRate = exchangeRate.BuyRate,
                Fee = fee,
                Status = RemittanceStatus.Processing,
                Purpose = purpose,
                PurposeDescription = purposeDescription,
                TrackingNumber = $"RM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(2)
            };

            _context.Remittances.Add(remittance);

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.TransferOut,
                Amount = sendAmount,
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.Balance,
                Description = $"International transfer to {recipientName} ({recipientCountry})",
                ReferenceNumber = remittance.TrackingNumber,
                Fee = fee,
                ExchangeRate = exchangeRate.BuyRate,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();

            // Simulate processing completion
            remittance.Status = RemittanceStatus.Completed;
            remittance.CompletedAt = DateTime.UtcNow;
            remittance.ExternalReference = $"EXT-{Guid.NewGuid().ToString()[..12].ToUpper()}";
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Remittance completed. Tracking: {Tracking}", remittance.TrackingNumber);
            return remittance;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Remittance>> GetUserRemittancesAsync(string userId, int page = 1, int pageSize = 20)
    {
        return await _context.Remittances
            .Where(r => r.SenderUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Remittance?> GetRemittanceByIdAsync(Guid id, string userId)
    {
        return await _context.Remittances
            .FirstOrDefaultAsync(r => r.Id == id && r.SenderUserId == userId);
    }

    public async Task<Remittance?> GetRemittanceByTrackingAsync(string trackingNumber)
    {
        return await _context.Remittances
            .FirstOrDefaultAsync(r => r.TrackingNumber == trackingNumber);
    }

    public async Task<List<string>> GetSupportedCountriesAsync()
    {
        return await _context.RemittancePartners
            .Where(rp => rp.IsActive)
            .Select(rp => rp.Country)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    private decimal CalculateFee(decimal amount)
    {
        if (amount <= 10000) return 500;
        if (amount <= 50000) return 1000;
        if (amount <= 200000) return 2500;
        return amount * 0.015m;
    }
}
