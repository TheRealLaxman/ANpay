using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class FeeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FeeService> _logger;

    public FeeService(ApplicationDbContext context, ILogger<FeeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Fee>> GetAllAsync()
    {
        return await _context.Fees.OrderByDescending(f => f.CreatedAt).ToListAsync();
    }

    public async Task<Fee> CreateAsync(Fee fee)
    {
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Fee created: {Name}", fee.Name);
        return fee;
    }

    public async Task<Fee> UpdateAsync(Guid id, Fee updated)
    {
        var fee = await _context.Fees.FindAsync(id)
            ?? throw new NotFoundException("Fee not found");

        fee.Name = updated.Name;
        fee.Type = updated.Type;
        fee.AppliesTo = updated.AppliesTo;
        fee.Value = updated.Value;
        fee.MinAmount = updated.MinAmount;
        fee.MaxAmount = updated.MaxAmount;
        fee.MinFee = updated.MinFee;
        fee.MaxFee = updated.MaxFee;
        fee.Currency = updated.Currency;
        fee.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Fee updated: {Id}", id);
        return fee;
    }

    public async Task DeleteAsync(Guid id)
    {
        var fee = await _context.Fees.FindAsync(id)
            ?? throw new NotFoundException("Fee not found");

        _context.Fees.Remove(fee);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Fee deleted: {Id}", id);
    }

    public async Task<decimal> CalculateFeeAsync(FeeAppliesTo appliesTo, decimal amount, string currency = "NGN")
    {
        var fee = await _context.Fees
            .FirstOrDefaultAsync(f => f.AppliesTo == appliesTo && f.IsActive && f.Currency == currency);

        if (fee == null) return 0;

        if (amount < fee.MinAmount || amount > fee.MaxAmount) return 0;

        decimal calculatedFee = fee.Type switch
        {
            FeeType.Percentage => amount * fee.Value / 100,
            FeeType.Fixed => fee.Value,
            _ => 0
        };

        return Math.Max(fee.MinFee, Math.Min(fee.MaxFee, calculatedFee));
    }
}
