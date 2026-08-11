using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class MerchantService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MerchantService> _logger;

    public MerchantService(ApplicationDbContext context, ILogger<MerchantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Merchant> RegisterAsync(string userId, string businessName, string businessType, string address, string phone, string email, string taxId)
    {
        if (await _context.Merchants.AnyAsync(m => m.UserId == userId))
            throw new ValidationException("User already has a merchant account");

        var merchant = new Merchant
        {
            UserId = userId,
            BusinessName = businessName,
            BusinessType = businessType,
            BusinessAddress = address,
            Phone = phone,
            ContactEmail = email,
            TaxId = taxId,
            Status = MerchantStatus.Pending
        };

        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Merchant registered: {BusinessName} by {UserId}", businessName, userId);
        return merchant;
    }

    public async Task<Merchant> ApproveAsync(Guid merchantId)
    {
        var merchant = await _context.Merchants.FindAsync(merchantId)
            ?? throw new NotFoundException("Merchant not found");

        merchant.Status = MerchantStatus.Active;
        merchant.ApprovedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Merchant approved: {Id}", merchantId);
        return merchant;
    }

    public async Task<Merchant> SuspendAsync(Guid merchantId)
    {
        var merchant = await _context.Merchants.FindAsync(merchantId)
            ?? throw new NotFoundException("Merchant not found");

        merchant.Status = MerchantStatus.Suspended;
        await _context.SaveChangesAsync();
        return merchant;
    }

    public async Task<Merchant?> GetByUserIdAsync(string userId)
    {
        return await _context.Merchants.FirstOrDefaultAsync(m => m.UserId == userId);
    }

    public async Task<Merchant> GetByIdAsync(Guid id)
    {
        return await _context.Merchants.FindAsync(id)
            ?? throw new NotFoundException("Merchant not found");
    }

    public async Task<List<Merchant>> GetAllAsync()
    {
        return await _context.Merchants
            .Include(m => m.User)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<MerchantPayment> CreatePaymentAsync(Guid merchantId, decimal amount, string description, string orderReference, string? customerId = null)
    {
        var merchant = await _context.Merchants.FindAsync(merchantId)
            ?? throw new NotFoundException("Merchant not found");

        if (merchant.Status != MerchantStatus.Active)
            throw new ValidationException("Merchant is not active");

        var commission = amount * merchant.CommissionRate;
        var netAmount = amount - commission;

        var payment = new MerchantPayment
        {
            MerchantId = merchantId,
            Amount = amount,
            Commission = commission,
            NetAmount = netAmount,
            Description = description,
            OrderReference = orderReference,
            CustomerId = customerId,
            Status = MerchantPaymentStatus.Pending
        };

        _context.MerchantPayments.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Merchant payment created: {MerchantId} amount {Amount}", merchantId, amount);
        return payment;
    }

    public async Task<MerchantPayment> CompletePaymentAsync(Guid paymentId, string paymentReference)
    {
        var payment = await _context.MerchantPayments.FindAsync(paymentId)
            ?? throw new NotFoundException("Payment not found");

        payment.Status = MerchantPaymentStatus.Completed;
        payment.PaymentReference = paymentReference;
        payment.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Merchant payment completed: {PaymentId}", paymentId);
        return payment;
    }

    public async Task<List<MerchantPayment>> GetMerchantPaymentsAsync(Guid merchantId)
    {
        return await _context.MerchantPayments
            .Where(mp => mp.MerchantId == merchantId)
            .OrderByDescending(mp => mp.CreatedAt)
            .ToListAsync();
    }

    public async Task<MerchantDashboardDto> GetDashboardAsync(Guid merchantId)
    {
        var merchant = await _context.Merchants.FindAsync(merchantId)
            ?? throw new NotFoundException("Merchant not found");

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var todayPayments = await _context.MerchantPayments
            .Where(mp => mp.MerchantId == merchantId && mp.CreatedAt >= today && mp.Status == MerchantPaymentStatus.Completed)
            .ToListAsync();

        var monthPayments = await _context.MerchantPayments
            .Where(mp => mp.MerchantId == merchantId && mp.CreatedAt >= monthStart && mp.Status == MerchantPaymentStatus.Completed)
            .ToListAsync();

        return new MerchantDashboardDto
        {
            BusinessName = merchant.BusinessName,
            Status = merchant.Status.ToString(),
            TodayTransactions = todayPayments.Count,
            TodayRevenue = todayPayments.Sum(p => p.Amount),
            TodayCommission = todayPayments.Sum(p => p.Commission),
            MonthTransactions = monthPayments.Count,
            MonthRevenue = monthPayments.Sum(p => p.Amount),
            MonthCommission = monthPayments.Sum(p => p.Commission),
            DailyLimit = merchant.DailyLimit,
            MonthlyLimit = merchant.MonthlyLimit,
            CommissionRate = merchant.CommissionRate
        };
    }

    public async Task<MerchantSettlement> CreateSettlementAsync(Guid merchantId, DateTime from, DateTime to)
    {
        var payments = await _context.MerchantPayments
            .Where(mp => mp.MerchantId == merchantId && mp.CreatedAt >= from && mp.CreatedAt <= to && mp.Status == MerchantPaymentStatus.Completed)
            .ToListAsync();

        var gross = payments.Sum(p => p.Amount);
        var commission = payments.Sum(p => p.Commission);
        var net = gross - commission;

        var settlement = new MerchantSettlement
        {
            MerchantId = merchantId,
            GrossAmount = gross,
            Commission = commission,
            NetAmount = net,
            PaymentCount = payments.Count,
            PeriodStart = from,
            PeriodEnd = to,
            Status = SettlementStatus.Pending
        };

        _context.MerchantSettlements.Add(settlement);
        await _context.SaveChangesAsync();
        return settlement;
    }
}

public class MerchantDashboardDto
{
    public string BusinessName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TodayTransactions { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal TodayCommission { get; set; }
    public int MonthTransactions { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal MonthCommission { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal CommissionRate { get; set; }
}
