using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using ANpay.Api.Data;
using ANpay.Api.Services;
using ANpay.Api.Models;
using ANpay.Api.DTOs;

namespace ANpay.Api.Tests;

public class WalletServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<WalletService>> _loggerMock;
    private readonly Mock<ILogger<LedgerService>> _ledgerLoggerMock;
    private readonly LedgerService _ledgerService;
    private readonly WalletService _walletService;

    public WalletServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<WalletService>>();
        _ledgerLoggerMock = new Mock<ILogger<LedgerService>>();
        _ledgerService = new LedgerService(_context, _ledgerLoggerMock.Object);
        _walletService = new WalletService(_context, _loggerMock.Object, _ledgerService);

        SeedData();
    }

    private void SeedData()
    {
        var account = new LedgerAccount
        {
            Name = "Customer Deposits",
            Code = "2000",
            Type = LedgerAccountType.Liability,
            Description = "Money owed to customers"
        };
        _context.LedgerAccounts.Add(account);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateWallet_ShouldCreateWalletWithZeroBalance()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var dto = new CreateWalletDto { WalletName = "Test Wallet", Currency = "USD" };
        var result = await _walletService.CreateWalletAsync(user.Id, dto);

        Assert.NotNull(result);
        Assert.Equal("Test Wallet", result.WalletName);
        Assert.Equal(0, result.Balance);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task GetUserWallets_ShouldReturnUserWallets()
    {
        var user = CreateUser("test@test.com");
        CreateWallet(user.Id, "USD");
        CreateWallet(user.Id, "EUR");
        await _context.SaveChangesAsync();

        var result = await _walletService.GetUserWalletsAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetWalletById_ShouldReturnWallet()
    {
        var user = CreateUser("test@test.com");
        var wallet = CreateWallet(user.Id, "USD");
        await _context.SaveChangesAsync();

        var result = await _walletService.GetWalletByIdAsync(wallet.Id, user.Id);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task Deposit_NegativeAmount_ShouldThrow()
    {
        var user = CreateUser("test@test.com");
        var wallet = CreateWallet(user.Id, "USD");
        await _context.SaveChangesAsync();

        var dto = new DepositDto { WalletId = wallet.Id, Amount = -100, Description = "Test" };

        await Assert.ThrowsAsync<ANpay.Api.Exceptions.ValidationException>(
            () => _walletService.DepositAsync(user.Id, dto));
    }

    [Fact]
    public async Task Withdraw_InsufficientBalance_ShouldThrow()
    {
        var user = CreateUser("test@test.com");
        var wallet = CreateWallet(user.Id, "USD");
        await _context.SaveChangesAsync();

        var dto = new WithdrawDto { WalletId = wallet.Id, Amount = 500, Description = "Test" };

        await Assert.ThrowsAsync<ANpay.Api.Exceptions.ValidationException>(
            () => _walletService.WithdrawAsync(user.Id, dto));
    }

    [Fact]
    public async Task Transfer_SameWallet_ShouldThrow()
    {
        var user = CreateUser("test@test.com");
        var wallet = CreateWallet(user.Id, "USD");
        await _context.SaveChangesAsync();

        var dto = new TransferDto
        {
            SourceWalletId = wallet.Id,
            DestinationWalletId = wallet.Id,
            Amount = 100,
            Description = "Test"
        };

        await Assert.ThrowsAsync<ANpay.Api.Exceptions.ValidationException>(
            () => _walletService.TransferAsync(user.Id, dto));
    }

    private ApplicationUser CreateUser(string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            Role = AppUserRole.Customer
        };
        _context.Users.Add(user);
        return user;
    }

    private Wallet CreateWallet(string userId, string currency, decimal balance = 0)
    {
        var wallet = new Wallet
        {
            UserId = userId,
            WalletName = $"{currency} Wallet",
            Currency = currency,
            Balance = balance
        };
        _context.Wallets.Add(wallet);
        return wallet;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
