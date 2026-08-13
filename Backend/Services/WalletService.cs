using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class WalletService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WalletService> _logger;
    private readonly LedgerService _ledgerService;

    public WalletService(ApplicationDbContext context, ILogger<WalletService> logger, LedgerService ledgerService)
    {
        _context = context;
        _logger = logger;
        _ledgerService = ledgerService;
    }

    public async Task<WalletDto> CreateWalletAsync(string userId, CreateWalletDto dto)
    {
        _logger.LogInformation("Creating wallet '{Name}' for user {UserId}", dto.WalletName, userId);

        var wallet = new Wallet
        {
            UserId = userId,
            WalletName = dto.WalletName,
            Currency = dto.Currency,
            Balance = 0
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Wallet {WalletId} created for user {UserId}", wallet.Id, userId);

        return new WalletDto
        {
            Id = wallet.Id,
            WalletName = wallet.WalletName,
            Balance = wallet.Balance,
            PendingBalance = wallet.PendingBalance,
            FrozenBalance = wallet.FrozenBalance,
            AvailableBalance = wallet.AvailableBalance,
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
                PendingBalance = w.PendingBalance,
                FrozenBalance = w.FrozenBalance,
                AvailableBalance = w.AvailableBalance,
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
            PendingBalance = wallet.PendingBalance,
            FrozenBalance = wallet.FrozenBalance,
            AvailableBalance = wallet.AvailableBalance,
            Currency = wallet.Currency,
            CreatedAt = wallet.CreatedAt
        };
    }

    public async Task<TransactionDto> DepositAsync(string userId, DepositDto dto)
    {
        _logger.LogInformation("Deposit of {Amount} to wallet {WalletId} by user {UserId}",
            dto.Amount, dto.WalletId, userId);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == dto.WalletId && w.UserId == userId);

            if (wallet == null)
                throw new NotFoundException("Wallet not found");

            if (dto.Amount <= 0)
                throw new ValidationException("Amount must be greater than zero");

            var balanceBefore = wallet.Balance;
            wallet.Balance += dto.Amount;

            var txRecord = new Transaction
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

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();

            try
            {
                await _ledgerService.PostWalletDepositAsync(txRecord.Id, wallet.Id, dto.Amount, wallet.Currency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post ledger entry for deposit {TxId}", txRecord.Id);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Deposit completed. Ref: {Ref}", txRecord.ReferenceNumber);

            return MapToTransactionDto(txRecord);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto)
    {
        _logger.LogInformation("Withdrawal of {Amount} from wallet {WalletId} by user {UserId}",
            dto.Amount, dto.WalletId, userId);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == dto.WalletId && w.UserId == userId);

            if (wallet == null)
                throw new NotFoundException("Wallet not found");

            if (dto.Amount <= 0)
                throw new ValidationException("Amount must be greater than zero");

            if (wallet.Balance < dto.Amount)
                throw new ValidationException("Insufficient balance");

            var balanceBefore = wallet.Balance;
            wallet.Balance -= dto.Amount;

            var txRecord = new Transaction
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

            _context.Transactions.Add(txRecord);
            await _context.SaveChangesAsync();

            try
            {
                await _ledgerService.PostWalletWithdrawalAsync(txRecord.Id, wallet.Id, dto.Amount, wallet.Currency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post ledger entry for withdrawal {TxId}", txRecord.Id);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Withdrawal completed. Ref: {Ref}", txRecord.ReferenceNumber);

            return MapToTransactionDto(txRecord);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TransactionDto> TransferAsync(string userId, TransferDto dto)
    {
        _logger.LogInformation("Transfer of {Amount} from {Src} to {Dest} by user {UserId}",
            dto.Amount, dto.SourceWalletId, dto.DestinationWalletId, userId);

        if (dto.SourceWalletId == dto.DestinationWalletId)
            throw new ValidationException("Cannot transfer to the same wallet");

        if (dto.Amount <= 0)
            throw new ValidationException("Amount must be greater than zero");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var sourceWallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == dto.SourceWalletId && w.UserId == userId);

            if (sourceWallet == null)
                throw new NotFoundException("Source wallet not found");

            var destWallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == dto.DestinationWalletId);

            if (destWallet == null)
                throw new NotFoundException("Destination wallet not found");

            if (sourceWallet.Balance < dto.Amount)
                throw new ValidationException("Insufficient balance");

            if (sourceWallet.Currency != destWallet.Currency)
                throw new ValidationException("Currency mismatch");

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

            try
            {
                await _ledgerService.PostTransferAsync(outTransaction.Id, sourceWallet.Id, destWallet.Id, dto.Amount, sourceWallet.Currency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to post ledger entry for transfer {Ref}", referenceNumber);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Transfer completed. Ref: {Ref}", referenceNumber);

            return MapToTransactionDto(outTransaction);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
            Fee = t.Fee,
            ExchangeRate = t.ExchangeRate,
            Channel = t.Channel,
            BranchId = t.BranchId,
            EmployeeId = t.EmployeeId,
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
