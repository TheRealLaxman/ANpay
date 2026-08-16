using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;
using ANpay.Api.Services.BillPaymentProvider;

namespace ANpay.Api.Services;

public class BillPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BillPaymentService> _logger;
    private readonly LedgerService _ledgerService;
    private readonly IBillPaymentProvider _billPaymentProvider;
    private readonly ISmsService _smsService;
    private readonly IEmailService _emailService;

    public BillPaymentService(
        ApplicationDbContext context,
        ILogger<BillPaymentService> logger,
        LedgerService ledgerService,
        IBillPaymentProvider billPaymentProvider,
        ISmsService smsService,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _ledgerService = ledgerService;
        _billPaymentProvider = billPaymentProvider;
        _smsService = smsService;
        _emailService = emailService;
    }

    public async Task<List<BillProvider>> GetProvidersAsync(BillCategory? category = null)
    {
        var query = _context.BillProviders.Where(bp => bp.IsActive);
        if (category.HasValue)
            query = query.Where(bp => bp.Category == category.Value);
        return await query.OrderBy(bp => bp.Name).ToListAsync();
    }

    public async Task<BillProvider?> GetProviderByCodeAsync(string code)
    {
        return await _context.BillProviders.FirstOrDefaultAsync(bp => bp.Code == code && bp.IsActive);
    }

    public async Task<BillPaymentResponseDto> ValidateBillAsync(string providerCode, string billerCode, string customerReference, decimal amount)
    {
        var provider = await _context.BillProviders.FirstOrDefaultAsync(bp => bp.Code == providerCode && bp.IsActive);
        if (provider == null) throw new NotFoundException("Bill provider not found");

        if (amount < provider.MinimumAmount || amount > provider.MaximumAmount)
            throw new ValidationException($"Amount must be between {provider.MinimumAmount} and {provider.MaximumAmount}");

        var request = new BillPaymentRequest
        {
            ProviderCode = providerCode,
            BillerCode = billerCode,
            CustomerReference = customerReference,
            Amount = amount,
            Currency = provider.Currency
        };

        var result = await _billPaymentProvider.ValidateAsync(request);

        return new BillPaymentResponseDto
        {
            Success = result.Success,
            Message = result.Message,
            Amount = result.Amount ?? amount,
            CustomerName = result.Metadata.GetValueOrDefault("customerName"),
            AccountDetails = result.Metadata.GetValueOrDefault("accountDetails")
        };
    }

    public async Task<BillPayment> PayBillAsync(string userId, Guid walletId, string providerCode, string billerCode, string customerReference, decimal amount, string? description = null)
    {
        _logger.LogInformation("Processing bill payment for user {UserId}, provider {Provider}", userId, providerCode);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        var provider = await _context.BillProviders.FirstOrDefaultAsync(bp => bp.Code == providerCode && bp.IsActive);
        if (provider == null) throw new NotFoundException("Bill provider not found");

        if (amount < provider.MinimumAmount || amount > provider.MaximumAmount)
            throw new ValidationException($"Amount must be between {provider.MinimumAmount} and {provider.MaximumAmount}");

        var fee = provider.FixedFee + (amount * provider.PercentageFee / 100);
        var totalDebit = amount + fee;

        if (wallet.Balance < totalDebit)
            throw new ValidationException("Insufficient balance");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = wallet.Balance;
            wallet.Balance -= totalDebit;

            var billPayment = new BillPayment
            {
                UserId = userId,
                WalletId = walletId,
                Category = provider.Category,
                Provider = provider.Name,
                BillerCode = billerCode,
                CustomerReference = customerReference,
                Amount = amount,
                Fee = fee,
                Currency = provider.Currency,
                Status = BillPaymentStatus.Processing,
                CustomerAccountNumber = customerReference,
                Channel = "App"
            };

            _context.BillPayments.Add(billPayment);

            var txRecord = new Transaction
            {
                WalletId = walletId,
                Type = TransactionType.Payment,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.Balance,
                Description = description ?? $"Bill payment to {provider.Name} ({billerCode})",
                ReferenceNumber = $"BIL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Fee = fee,
                Status = TransactionStatus.Completed
            };

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();

            try
            {
                await _ledgerService.PostWalletWithdrawalAsync(txRecord.Id, walletId, totalDebit, wallet.Currency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post ledger entry for bill payment");
            }

            // Call external bill payment provider
            var paymentRequest = new BillPaymentRequest
            {
                ProviderCode = providerCode,
                BillerCode = billerCode,
                CustomerReference = customerReference,
                Amount = amount,
                Currency = provider.Currency,
                Description = description
            };

            var paymentResult = await _billPaymentProvider.PayAsync(paymentRequest);

            if (paymentResult.Success)
            {
                billPayment.Status = BillPaymentStatus.Completed;
                billPayment.TransactionId = paymentResult.TransactionId ?? paymentResult.Reference;
                billPayment.CompletedAt = DateTime.UtcNow;
                billPayment.ResponseMessage = paymentResult.Message;
                billPayment.PaymentReference = paymentResult.Reference;
                txRecord.AuthorizationInfo = paymentResult.Reference;

                // Store token/PIN for airtime/data purchases
                if (!string.IsNullOrEmpty(paymentResult.Token))
                {
                    billPayment.TokenOrPin = paymentResult.Token;
                }

                _logger.LogInformation("Bill payment completed. Ref: {Ref}", txRecord.ReferenceNumber);

                // Send notifications
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    await _smsService.SendTransactionAlertAsync(
                        user.PhoneNumber ?? "", "Bill Payment", amount, wallet.Currency, txRecord.ReferenceNumber);

                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        await _emailService.SendTransactionReceiptAsync(
                            user.Email, user.FirstName, "Bill Payment", amount, wallet.Currency, txRecord.ReferenceNumber);
                    }
                }
            }
            else
            {
                billPayment.Status = BillPaymentStatus.Failed;
                billPayment.ResponseMessage = paymentResult.Message;

                // Refund wallet
                wallet.Balance += totalDebit;
                txRecord.Status = TransactionStatus.Failed;

                _logger.LogWarning("Bill payment failed. Ref: {Ref}. Reason: {Reason}", txRecord.ReferenceNumber, paymentResult.Message);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return billPayment;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<BillPayment>> GetUserBillPaymentsAsync(string userId, int page = 1, int pageSize = 20)
    {
        return await _context.BillPayments
            .Where(bp => bp.UserId == userId)
            .OrderByDescending(bp => bp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<BillPayment?> GetBillPaymentByIdAsync(Guid id, string userId)
    {
        return await _context.BillPayments
            .FirstOrDefaultAsync(bp => bp.Id == id && bp.UserId == userId);
    }

    public async Task<BillPayment?> CheckPaymentStatusAsync(Guid billPaymentId, string userId)
    {
        var billPayment = await _context.BillPayments
            .FirstOrDefaultAsync(bp => bp.Id == billPaymentId && bp.UserId == userId);

        if (billPayment == null) return null;

        if (billPayment.Status == BillPaymentStatus.Processing && !string.IsNullOrEmpty(billPayment.PaymentReference))
        {
            var statusResult = await _billPaymentProvider.CheckStatusAsync(billPayment.PaymentReference);

            if (statusResult.Status == BillProviderPaymentStatus.Completed)
            {
                billPayment.Status = BillPaymentStatus.Completed;
                billPayment.CompletedAt = DateTime.UtcNow;
            }
            else if (statusResult.Status == BillProviderPaymentStatus.Failed)
            {
                billPayment.Status = BillPaymentStatus.Failed;
                billPayment.ResponseMessage = statusResult.Message;

                // Refund wallet
                var wallet = await _context.Wallets.FindAsync(billPayment.WalletId);
                if (wallet != null)
                {
                    wallet.Balance += billPayment.Amount + billPayment.Fee;
                }
            }

            await _context.SaveChangesAsync();
        }

        return billPayment;
    }

    public async Task<BillProvider> CreateProviderAsync(string name, BillCategory category, string code, decimal minAmount, decimal maxAmount, decimal fixedFee, decimal percentageFee, string currency = "NGN")
    {
        var provider = new BillProvider
        {
            Name = name,
            Category = category,
            Code = code,
            MinimumAmount = minAmount,
            MaximumAmount = maxAmount,
            FixedFee = fixedFee,
            PercentageFee = percentageFee,
            Currency = currency
        };

        _context.BillProviders.Add(provider);
        await _context.SaveChangesAsync();
        return provider;
    }
}

public class BillPaymentResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? AccountDetails { get; set; }
}
