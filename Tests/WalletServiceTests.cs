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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
        _context.LedgerAccounts.Add(new LedgerAccount
        {
            Name = "Customer Deposits",
            Code = "2000",
            Type = LedgerAccountType.Liability,
            Description = "Money owed to customers"
        });
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
        Assert.Equal(0, result.PendingBalance);
        Assert.Equal(0, result.FrozenBalance);
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

    [Fact]
    public async Task CreateWallet_DefaultValues_ShouldHaveZeroPendingAndFrozen()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var dto = new CreateWalletDto { WalletName = "Savings", Currency = "NPR" };
        var result = await _walletService.CreateWalletAsync(user.Id, dto);

        Assert.Equal(0, result.PendingBalance);
        Assert.Equal(0, result.FrozenBalance);
        Assert.Equal(result.Balance - result.FrozenBalance, result.AvailableBalance);
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

    public void Dispose() => _context.Dispose();
}

public class FraudServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<FraudService>> _loggerMock;
    private readonly FraudService _fraudService;

    public FraudServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<FraudService>>();
        _fraudService = new FraudService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task CalculateRiskScore_LowRisk_ShouldReturnLowScore()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var score = await _fraudService.CalculateRiskScoreAsync(user.Id, 10m, "192.168.1.1", "Chrome/Windows");

        // Unknown IP (+15) + Unknown Device (+10) = 25 minimum for new user with small amount
        Assert.True(score <= 35);
    }

    [Fact]
    public async Task CalculateRiskScore_LargeAmount_ShouldIncreaseScore()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var score = await _fraudService.CalculateRiskScoreAsync(user.Id, 100000m, "192.168.1.1", "Chrome/Windows");

        Assert.True(score >= 25);
    }

    [Fact]
    public async Task CreateAlert_ShouldCreateFraudAlert()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var alert = await _fraudService.CreateAlertAsync(
            user.Id, FraudAlertType.SuspiciousLogin, 75, "Test alert", "192.168.1.1", "Chrome");

        Assert.NotNull(alert);
        Assert.Equal(FraudAlertStatus.Open, alert.Status);
        Assert.Equal(75, alert.RiskScore);
    }

    [Fact]
    public async Task GetOpenAlerts_ShouldReturnOpenAlerts()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        await _fraudService.CreateAlertAsync(user.Id, FraudAlertType.SuspiciousLogin, 75, "Alert 1");
        await _fraudService.CreateAlertAsync(user.Id, FraudAlertType.HighRiskTransaction, 80, "Alert 2");

        var alerts = await _fraudService.GetOpenAlertsAsync();

        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public async Task UpdateAlertStatus_ShouldChangeStatus()
    {
        var user = CreateUser("test@test.com");
        await _context.SaveChangesAsync();

        var alert = await _fraudService.CreateAlertAsync(user.Id, FraudAlertType.SuspiciousLogin, 75, "Test");
        await _fraudService.UpdateAlertStatusAsync(alert.Id, FraudAlertStatus.Resolved, "Fixed");

        var updated = await _fraudService.GetAlertByIdAsync(alert.Id);
        Assert.NotNull(updated);
        Assert.Equal(FraudAlertStatus.Resolved, updated.Status);
        Assert.Equal("Fixed", updated.Resolution);
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

    public void Dispose() => _context.Dispose();
}

public class ReconciliationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ReconciliationService>> _loggerMock;
    private readonly ReconciliationService _reconciliationService;

    public ReconciliationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ReconciliationService>>();
        _reconciliationService = new ReconciliationService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task RunReconciliation_MatchingBalance_ShouldReturnMatched()
    {
        var record = await _reconciliationService.RunReconciliationAsync(
            ReconciliationType.WalletToBank,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            "Bank Statement",
            0m);

        Assert.NotNull(record);
        Assert.True(record.IsMatched);
        Assert.Equal(ReconciliationStatus.Matched, record.Status);
    }

    [Fact]
    public async Task RunReconciliation_Mismatch_ShouldReturnDiscrepancy()
    {
        var record = await _reconciliationService.RunReconciliationAsync(
            ReconciliationType.WalletToBank,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            "Bank Statement",
            1000m);

        Assert.NotNull(record);
        Assert.False(record.IsMatched);
        Assert.Equal(ReconciliationStatus.DiscrepancyFound, record.Status);
    }

    [Fact]
    public async Task GetRecords_ShouldReturnRecords()
    {
        await _reconciliationService.RunReconciliationAsync(
            ReconciliationType.WalletToBank,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            "Bank Statement",
            0m);

        var records = await _reconciliationService.GetRecordsAsync();

        Assert.Single(records);
    }

    public void Dispose() => _context.Dispose();
}
