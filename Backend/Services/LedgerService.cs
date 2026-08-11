using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class LedgerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(ApplicationDbContext context, ILogger<LedgerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAccountsAsync()
    {
        if (await _context.LedgerAccounts.AnyAsync()) return;

        var accounts = new List<LedgerAccount>
        {
            new() { Name = "Cash", Code = "1000", Type = LedgerAccountType.Asset, Description = "Physical cash" },
            new() { Name = "Customer Wallets", Code = "1100", Type = LedgerAccountType.Asset, Description = "Customer wallet balances" },
            new() { Name = "Bank Account", Code = "1200", Type = LedgerAccountType.Asset, Description = "Company bank account" },
            new() { Name = "Pending Transactions", Code = "1300", Type = LedgerAccountType.Asset, Description = "Transactions in progress" },

            new() { Name = "Customer Deposits", Code = "2000", Type = LedgerAccountType.Liability, Description = "Money owed to customers" },
            new() { Name = "Fees Payable", Code = "2100", Type = LedgerAccountType.Liability, Description = "Fees owed to partners" },

            new() { Name = "Share Capital", Code = "3000", Type = LedgerAccountType.Equity, Description = "Company equity" },
            new() { Name = "Retained Earnings", Code = "3100", Type = LedgerAccountType.Equity, Description = "Accumulated profits" },

            new() { Name = "Fee Revenue", Code = "4000", Type = LedgerAccountType.Revenue, Description = "Transaction fees earned" },
            new() { Name = "Exchange Revenue", Code = "4100", Type = LedgerAccountType.Revenue, Description = "Exchange spread earned" },
            new() { Name = "Interest Income", Code = "4200", Type = LedgerAccountType.Revenue, Description = "Interest earned" },

            new() { Name = "Operating Expenses", Code = "5000", Type = LedgerAccountType.Expense, Description = "General operating costs" },
            new() { Name = "Transaction Costs", Code = "5100", Type = LedgerAccountType.Expense, Description = "Cost of processing transactions" },
        };

        _context.LedgerAccounts.AddRange(accounts);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Ledger accounts seeded");
    }

    public async Task PostTransactionAsync(Guid transactionId, string currency,
        Guid debitAccountId, Guid creditAccountId, decimal amount, string description)
    {
        var entry = new LedgerEntry
        {
            TransactionId = transactionId,
            DebitAccountId = debitAccountId,
            CreditAccountId = creditAccountId,
            Amount = amount,
            Currency = currency,
            Description = description
        };

        _context.LedgerEntries.Add(entry);
        await _context.SaveChangesAsync();
    }

    public async Task PostWalletDepositAsync(Guid transactionId, Guid walletId, decimal amount, string currency)
    {
        var walletAccount = await GetOrCreateWalletAccountAsync(walletId);
        var customerDeposits = await GetAccountByCodeAsync("2000");

        await PostTransactionAsync(transactionId, currency, walletAccount.Id, customerDeposits.Id, amount, "Wallet deposit");
    }

    public async Task PostWalletWithdrawalAsync(Guid transactionId, Guid walletId, decimal amount, string currency)
    {
        var walletAccount = await GetOrCreateWalletAccountAsync(walletId);
        var customerDeposits = await GetAccountByCodeAsync("2000");

        await PostTransactionAsync(transactionId, currency, customerDeposits.Id, walletAccount.Id, amount, "Wallet withdrawal");
    }

    public async Task PostTransferAsync(Guid transactionId, Guid sourceWalletId, Guid destWalletId, decimal amount, string currency)
    {
        var sourceAccount = await GetOrCreateWalletAccountAsync(sourceWalletId);
        var destAccount = await GetOrCreateWalletAccountAsync(destWalletId);

        await PostTransactionAsync(transactionId, currency, sourceAccount.Id, destAccount.Id, amount, "Wallet transfer");
    }

    public async Task PostFeeAsync(Guid transactionId, decimal feeAmount, string currency)
    {
        var feeRevenue = await GetAccountByCodeAsync("4000");
        var customerDeposits = await GetAccountByCodeAsync("2000");

        await PostTransactionAsync(transactionId, currency, customerDeposits.Id, feeRevenue.Id, feeAmount, "Transaction fee");
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        var debits = await _context.LedgerEntries
            .Where(le => le.DebitAccountId == accountId)
            .SumAsync(le => le.Amount);

        var credits = await _context.LedgerEntries
            .Where(le => le.CreditAccountId == accountId)
            .SumAsync(le => le.Amount);

        var account = await _context.LedgerAccounts.FindAsync(accountId);
        return account?.Type switch
        {
            LedgerAccountType.Asset => debits - credits,
            LedgerAccountType.Expense => debits - credits,
            LedgerAccountType.Liability => credits - debits,
            LedgerAccountType.Equity => credits - debits,
            LedgerAccountType.Revenue => credits - debits,
            _ => 0
        };
    }

    private async Task<LedgerAccount> GetOrCreateWalletAccountAsync(Guid walletId)
    {
        var code = $"1101-{walletId.ToString()[..8]}";
        var account = await _context.LedgerAccounts.FirstOrDefaultAsync(a => a.Code == code);
        if (account == null)
        {
            account = new LedgerAccount
            {
                Name = $"Wallet {walletId.ToString()[..8]}",
                Code = code,
                Type = LedgerAccountType.Asset,
                Description = $"Ledger account for wallet {walletId}"
            };
            _context.LedgerAccounts.Add(account);
            await _context.SaveChangesAsync();
        }
        return account;
    }

    private async Task<LedgerAccount> GetAccountByCodeAsync(string code)
    {
        return await _context.LedgerAccounts.FirstOrDefaultAsync(a => a.Code == code)
            ?? throw new NotFoundException($"Ledger account {code} not found");
    }
}
