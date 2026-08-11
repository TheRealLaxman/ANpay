using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class TransactionService
{
    private readonly ApplicationDbContext _context;

    public TransactionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TransactionHistoryDto> GetTransactionHistoryAsync(
        Guid walletId,
        string userId,
        int page = 1,
        int pageSize = 20)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);

        if (wallet == null)
            throw new Exception("Wallet not found");

        var query = _context.Transactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var transactions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                Description = t.Description,
                ReferenceNumber = t.ReferenceNumber,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                DestinationWalletName = t.DestinationWallet != null
                    ? t.DestinationWallet.WalletName
                    : null
            })
            .ToListAsync();

        return new TransactionHistoryDto
        {
            Transactions = transactions,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TransactionDto?> GetTransactionByIdAsync(Guid transactionId, string userId)
    {
        return await _context.Transactions
            .Include(t => t.Wallet)
            .Include(t => t.DestinationWallet)
            .Where(t => t.Id == transactionId && t.Wallet.UserId == userId)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                Description = t.Description,
                ReferenceNumber = t.ReferenceNumber,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                DestinationWalletName = t.DestinationWallet != null
                    ? t.DestinationWallet.WalletName
                    : null
            })
            .FirstOrDefaultAsync();
    }
}
