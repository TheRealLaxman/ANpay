using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;
using System.Security.Cryptography;

namespace ANpay.Api.Services;

public class VirtualCardService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VirtualCardService> _logger;

    public VirtualCardService(ApplicationDbContext context, ILogger<VirtualCardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VirtualCard> CreateCardAsync(string userId, Guid walletId, VirtualCardType cardType, string cardHolderName, decimal dailyLimit, decimal monthlyLimit)
    {
        _logger.LogInformation("Creating virtual card for user {UserId}", userId);

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found");

        var cardNumber = GenerateCardNumber();
        var expiryMonth = DateTime.UtcNow.AddMonths(cardType == VirtualCardType.Disposable ? 3 : 36).ToString("MM");
        var expiryYear = DateTime.UtcNow.AddMonths(cardType == VirtualCardType.Disposable ? 3 : 36).ToString("yyyy");

        var card = new VirtualCard
        {
            UserId = userId,
            WalletId = walletId,
            CardNumber = cardNumber,
            CardToken = Guid.NewGuid().ToString("N"),
            CardHolderName = cardHolderName,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = wallet.Currency,
            CardType = cardType,
            DailyLimit = dailyLimit,
            MonthlyLimit = monthlyLimit
        };

        _context.VirtualCards.Add(card);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Virtual card {CardId} created for user {UserId}", card.Id, userId);
        return card;
    }

    public async Task<List<VirtualCard>> GetUserCardsAsync(string userId)
    {
        return await _context.VirtualCards
            .Where(vc => vc.UserId == userId)
            .OrderByDescending(vc => vc.CreatedAt)
            .ToListAsync();
    }

    public async Task<VirtualCard?> GetCardByIdAsync(Guid cardId, string userId)
    {
        return await _context.VirtualCards
            .FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
    }

    public async Task<VirtualCard> FreezeCardAsync(Guid cardId, string userId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.Status = VirtualCardStatus.Frozen;
        card.LockedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Virtual card {CardId} frozen by user {UserId}", cardId, userId);
        return card;
    }

    public async Task<VirtualCard> UnfreezeCardAsync(Guid cardId, string userId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.Status = VirtualCardStatus.Active;
        card.LockedAt = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Virtual card {CardId} unfrozen by user {UserId}", cardId, userId);
        return card;
    }

    public async Task<VirtualCard> UpdateCardLimitsAsync(Guid cardId, string userId, decimal dailyLimit, decimal monthlyLimit)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.DailyLimit = dailyLimit;
        card.MonthlyLimit = monthlyLimit;
        await _context.SaveChangesAsync();

        return card;
    }

    public async Task<VirtualCard> ToggleOnlinePaymentsAsync(Guid cardId, string userId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.AllowOnlinePayments = !card.AllowOnlinePayments;
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task<VirtualCard> ToggleAtmWithdrawalsAsync(Guid cardId, string userId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.AllowAtmWithdrawals = !card.AllowAtmWithdrawals;
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task<VirtualCard> CloseCardAsync(Guid cardId, string userId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.Status = VirtualCardStatus.Closed;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Virtual card {CardId} closed by user {UserId}", cardId, userId);
        return card;
    }

    public async Task<List<VirtualCardTransaction>> GetCardTransactionsAsync(Guid cardId, string userId, int page = 1, int pageSize = 20)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId && vc.UserId == userId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        return await _context.VirtualCardTransactions
            .Where(vct => vct.VirtualCardId == cardId)
            .OrderByDescending(vct => vct.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<VirtualCard> ResetDailySpendAsync(Guid cardId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.CurrentDaySpent = 0;
        card.LastResetDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task<VirtualCard> ResetMonthlySpendAsync(Guid cardId)
    {
        var card = await _context.VirtualCards.FirstOrDefaultAsync(vc => vc.Id == cardId);
        if (card == null) throw new NotFoundException("Virtual card not found");

        card.CurrentMonthSpent = 0;
        card.LastResetDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return card;
    }

    private string GenerateCardNumber()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var digits = new string(bytes.Select(b => (b % 10).ToString()[0]).ToArray());
        return $"{digits[..4]}-{digits[4..8]}-{digits[8..12]}-{digits[12..]}";
    }
}
