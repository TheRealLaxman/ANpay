using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class WalletService
{
    private readonly ApplicationDbContext _context;

    public WalletService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WalletDto> CreateWalletAsync(string userId, CreateWalletDto dto)
    {
        var wallet = new Wallet
        {
            UserId = userId,
            WalletName = dto.WalletName,
            Currency = dto.Currency,
            Balance = 0
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        return new WalletDto
        {
            Id = wallet.Id,
            WalletName = wallet.WalletName,
            Balance = wallet.Balance,
            Currency = wallet.Currency,
            CreatedAt = wallet.CreatedAt
        };
    }

    public async Task<List<WalletDto>> GetUserWalletsAsync(string userId)
    {
        return await _context.Wallets
            .Where(w => w.UserId == userId && w.IsActive)
            .Select(w => new WalletDto
            {
                Id = w.Id,
                WalletName = w.WalletName,
                Balance = w.Balance,
                Currency = w.Currency,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<WalletDto?> GetWalletByIdAsync(Guid walletId, string userId)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);

        if (wallet == null) return null;

        return new WalletDto
        {
            Id = wallet.Id,
            WalletName = wallet.WalletName,
            Balance = wallet.Balance,
            Currency = wallet.Currency,
            CreatedAt = wallet.CreatedAt
        };
    }

    public async Task<TransactionDto> DepositAsync(string userId, DepositDto dto)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.WalletId && w.UserId == userId);

        if (wallet == null)
            throw new Exception("Wallet not found");

        if (dto.Amount <= 0)
            throw new Exception("Amount must be greater than zero");

        var balanceBefore = wallet.Balance;
        wallet.Balance += dto.Amount;

        var transaction = new Transaction
        {
            WalletId = wallet.Id,
            Type = TransactionType.Deposit,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = dto.Description,
            ReferenceNumber = GenerateReferenceNumber(),
            Status = TransactionStatus.Completed
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return MapToTransactionDto(transaction);
    }

    public async Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.WalletId && w.UserId == userId);

        if (wallet == null)
            throw new Exception("Wallet not found");

        if (dto.Amount <= 0)
            throw new Exception("Amount must be greater than zero");

        if (wallet.Balance < dto.Amount)
            throw new Exception("Insufficient balance");

        var balanceBefore = wallet.Balance;
        wallet.Balance -= dto.Amount;

        var transaction = new Transaction
        {
            WalletId = wallet.Id,
            Type = TransactionType.Withdrawal,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = dto.Description,
            ReferenceNumber = GenerateReferenceNumber(),
            Status = TransactionStatus.Completed
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return MapToTransactionDto(transaction);
    }

    public async Task<TransactionDto> TransferAsync(string userId, TransferDto dto)
    {
        if (dto.SourceWalletId == dto.DestinationWalletId)
            throw new Exception("Cannot transfer to the same wallet");

        if (dto.Amount <= 0)
            throw new Exception("Amount must be greater than zero");

        var sourceWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.SourceWalletId && w.UserId == userId);

        if (sourceWallet == null)
            throw new Exception("Source wallet not found");

        var destWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.DestinationWalletId);

        if (destWallet == null)
            throw new Exception("Destination wallet not found");

        if (sourceWallet.Balance < dto.Amount)
            throw new Exception("Insufficient balance");

        if (sourceWallet.Currency != destWallet.Currency)
            throw new Exception("Currency mismatch");

        var sourceBalanceBefore = sourceWallet.Balance;
        var destBalanceBefore = destWallet.Balance;

        sourceWallet.Balance -= dto.Amount;
        destWallet.Balance += dto.Amount;

        var referenceNumber = GenerateReferenceNumber();

        var outTransaction = new Transaction
        {
            WalletId = sourceWallet.Id,
            Type = TransactionType.TransferOut,
            Amount = dto.Amount,
            BalanceBefore = sourceBalanceBefore,
            BalanceAfter = sourceWallet.Balance,
            Description = dto.Description,
            ReferenceNumber = referenceNumber,
            DestinationWalletId = destWallet.Id,
            Status = TransactionStatus.Completed
        };

        var inTransaction = new Transaction
        {
            WalletId = destWallet.Id,
            Type = TransactionType.TransferIn,
            Amount = dto.Amount,
            BalanceBefore = destBalanceBefore,
            BalanceAfter = destWallet.Balance,
            Description = $"Transfer from {sourceWallet.WalletName}",
            ReferenceNumber = referenceNumber,
            Status = TransactionStatus.Completed
        };

        _context.Transactions.Add(outTransaction);
        _context.Transactions.Add(inTransaction);
        await _context.SaveChangesAsync();

        return MapToTransactionDto(outTransaction);
    }

    private TransactionDto MapToTransactionDto(Transaction t)
    {
        return new TransactionDto
        {
            Id = t.Id,
            Type = t.Type.ToString(),
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            Description = t.Description,
            ReferenceNumber = t.ReferenceNumber,
            Status = t.Status.ToString(),
            CreatedAt = t.CreatedAt
        };
    }

    private string GenerateReferenceNumber()
    {
        return $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
