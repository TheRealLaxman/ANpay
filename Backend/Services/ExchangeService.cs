using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class ExchangeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExchangeService> _logger;

    public ExchangeService(ApplicationDbContext context, ILogger<ExchangeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ExchangeRate>> GetAllAsync()
    {
        return await _context.ExchangeRates
            .Where(er => er.IsActive)
            .OrderBy(er => er.FromCurrency)
            .ToListAsync();
    }

    public async Task<ExchangeRate> GetRateAsync(string from, string to)
    {
        return await _context.ExchangeRates
            .FirstOrDefaultAsync(er => er.FromCurrency == from && er.ToCurrency == to && er.IsActive)
            ?? throw new NotFoundException($"Exchange rate {from}/{to} not found");
    }

    public async Task<ExchangeRate> UpsertRateAsync(string from, string to, decimal buyRate, decimal sellRate)
    {
        var rate = await _context.ExchangeRates
            .FirstOrDefaultAsync(er => er.FromCurrency == from && er.ToCurrency == to);

        if (rate == null)
        {
            rate = new ExchangeRate
            {
                FromCurrency = from,
                ToCurrency = to,
                BuyRate = buyRate,
                SellRate = sellRate
            };
            _context.ExchangeRates.Add(rate);
        }
        else
        {
            rate.BuyRate = buyRate;
            rate.SellRate = sellRate;
            rate.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Exchange rate updated: {From}/{To} Buy={Buy} Sell={Sell}", from, to, buyRate, sellRate);
        return rate;
    }

    public async Task<ExchangeQuoteDto> GetQuoteAsync(string from, string to, decimal amount)
    {
        var rate = await GetRateAsync(from, to);
        var convertedAmount = amount * rate.BuyRate;

        return new ExchangeQuoteDto
        {
            FromCurrency = from,
            ToCurrency = to,
            Amount = amount,
            Rate = rate.BuyRate,
            ConvertedAmount = convertedAmount,
            Fee = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
    }
}

public class ExchangeQuoteDto
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal Fee { get; set; }
    public DateTime ExpiresAt { get; set; }
}
