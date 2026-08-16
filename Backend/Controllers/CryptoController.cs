using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CryptoController : ControllerBase
{
    private readonly CryptoService _cryptoService;
    private readonly AuthService _authService;
    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public CryptoController(CryptoService cryptoService, AuthService authService)
    {
        _cryptoService = cryptoService;
        _authService = authService;
    }

    [HttpGet("wallets")]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _cryptoService.GetUserWalletsAsync(UserId);
        return Ok(wallets);
    }

    [HttpPost("wallets")]
    public async Task<IActionResult> CreateWallet([FromBody] CreateCryptoWalletDto dto)
    {
        var wallet = await _cryptoService.CreateWalletAsync(UserId, dto.Asset, dto.Network);
        return Ok(wallet);
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] CryptoDepositDto dto)
    {
        var tx = await _cryptoService.DepositAsync(UserId, dto.WalletId, dto.Amount, dto.TxHash);
        return Ok(tx);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] CryptoWithdrawDto dto)
    {
        if (UserId == null) return Unauthorized();
        if (string.IsNullOrEmpty(dto.TransactionPin))
            return BadRequest(new { message = "Transaction PIN is required" });

        var pinValid = await _authService.VerifyTransactionPinAsync(UserId, dto.TransactionPin);
        if (!pinValid) return Unauthorized(new { message = "Invalid transaction PIN" });

        var tx = await _cryptoService.WithdrawAsync(UserId, dto.WalletId, dto.Amount, dto.ToAddress);
        return Ok(tx);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] Guid? walletId)
    {
        var txs = await _cryptoService.GetTransactionsAsync(UserId, walletId);
        return Ok(txs);
    }

    [HttpGet("networks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNetworkConfigs()
    {
        var configs = await _cryptoService.GetNetworkConfigsAsync();
        return Ok(configs);
    }

    [HttpPost("networks")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpsertNetworkConfig([FromBody] CryptoNetworkConfig config)
    {
        var result = await _cryptoService.UpsertNetworkConfigAsync(config);
        return Ok(result);
    }
}

public class CreateCryptoWalletDto
{
    public CryptoAsset Asset { get; set; }
    public CryptoNetwork Network { get; set; }
}

public class CryptoDepositDto
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string TxHash { get; set; } = string.Empty;
}

public class CryptoWithdrawDto
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string? TransactionPin { get; set; }
}
