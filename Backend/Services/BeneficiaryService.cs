using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class BeneficiaryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BeneficiaryService> _logger;

    public BeneficiaryService(ApplicationDbContext context, ILogger<BeneficiaryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BeneficiaryDto>> GetUserBeneficiariesAsync(string userId)
    {
        return await _context.Beneficiaries
            .Where(b => b.UserId == userId && b.IsActive)
            .Include(b => b.Wallet)
            .Select(b => new BeneficiaryDto
            {
                Id = b.Id,
                Nickname = b.Nickname,
                WalletId = b.WalletId,
                WalletName = b.Wallet.WalletName,
                WalletCurrency = b.Wallet.Currency,
                Email = b.Email,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<BeneficiaryDto> CreateBeneficiaryAsync(string userId, CreateBeneficiaryDto dto)
    {
        _logger.LogInformation("Creating beneficiary '{Nickname}' for user {UserId}", dto.Nickname, userId);

        var walletExists = await _context.Wallets.AnyAsync(w => w.Id == dto.WalletId && w.IsActive);
        if (!walletExists)
            throw new NotFoundException("Destination wallet not found");

        var existing = await _context.Beneficiaries
            .FirstOrDefaultAsync(b => b.UserId == userId && b.WalletId == dto.WalletId && b.IsActive);
        if (existing != null)
            throw new ValidationException("Beneficiary already exists for this wallet");

        var beneficiary = new Beneficiary
        {
            UserId = userId,
            Nickname = dto.Nickname,
            WalletId = dto.WalletId,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow
        };

        _context.Beneficiaries.Add(beneficiary);
        await _context.SaveChangesAsync();

        var wallet = await _context.Wallets.FindAsync(dto.WalletId);

        _logger.LogInformation("Beneficiary {Id} created for user {UserId}", beneficiary.Id, userId);

        return new BeneficiaryDto
        {
            Id = beneficiary.Id,
            Nickname = beneficiary.Nickname,
            WalletId = beneficiary.WalletId,
            WalletName = wallet?.WalletName ?? string.Empty,
            WalletCurrency = wallet?.Currency ?? string.Empty,
            Email = beneficiary.Email,
            CreatedAt = beneficiary.CreatedAt
        };
    }

    public async Task DeleteBeneficiaryAsync(Guid beneficiaryId, string userId)
    {
        _logger.LogInformation("Deleting beneficiary {Id} for user {UserId}", beneficiaryId, userId);

        var beneficiary = await _context.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.UserId == userId);

        if (beneficiary == null)
            throw new NotFoundException("Beneficiary not found");

        beneficiary.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Beneficiary {Id} deleted", beneficiaryId);
    }
}
