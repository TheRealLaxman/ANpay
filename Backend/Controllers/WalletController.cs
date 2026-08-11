using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly ILogger<WalletController> _logger;

    public WalletController(WalletService walletService, ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<WalletDto>> CreateWallet([FromBody] CreateWalletDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var wallet = await _walletService.CreateWalletAsync(userId, dto);
        return CreatedAtAction(nameof(GetWallet), new { id = wallet.Id }, wallet);
    }

    [HttpGet]
    public async Task<ActionResult<List<WalletDto>>> GetWallets()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var wallets = await _walletService.GetUserWalletsAsync(userId);
        return Ok(wallets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WalletDto>> GetWallet(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var wallet = await _walletService.GetWalletByIdAsync(id, userId);
        if (wallet == null)
            return NotFound();
        return Ok(wallet);
    }

    [HttpPost("deposit")]
    [Authorize(Roles = "Customer,Official,BranchAdmin,SuperAdmin")]
    public async Task<ActionResult<TransactionDto>> Deposit([FromBody] DepositDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transaction = await _walletService.DepositAsync(userId, dto);
        return Ok(transaction);
    }

    [HttpPost("withdraw")]
    [Authorize(Roles = "Customer,Official,BranchAdmin,SuperAdmin")]
    public async Task<ActionResult<TransactionDto>> Withdraw([FromBody] WithdrawDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transaction = await _walletService.WithdrawAsync(userId, dto);
        return Ok(transaction);
    }

    [HttpPost("transfer")]
    [Authorize(Roles = "Customer,Official,BranchAdmin,SuperAdmin")]
    public async Task<ActionResult<TransactionDto>> Transfer([FromBody] TransferDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transaction = await _walletService.TransferAsync(userId, dto);
        return Ok(transaction);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
