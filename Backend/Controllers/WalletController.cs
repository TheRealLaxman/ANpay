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

    public WalletController(WalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpPost]
    public async Task<ActionResult<WalletDto>> CreateWallet([FromBody] CreateWalletDto dto)
    {
        var userId = GetUserId();
        var wallet = await _walletService.CreateWalletAsync(userId, dto);
        return CreatedAtAction(nameof(GetWallet), new { id = wallet.Id }, wallet);
    }

    [HttpGet]
    public async Task<ActionResult<List<WalletDto>>> GetWallets()
    {
        var userId = GetUserId();
        var wallets = await _walletService.GetUserWalletsAsync(userId);
        return Ok(wallets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WalletDto>> GetWallet(Guid id)
    {
        var userId = GetUserId();
        var wallet = await _walletService.GetWalletByIdAsync(id, userId);
        if (wallet == null)
            return NotFound();
        return Ok(wallet);
    }

    [HttpPost("deposit")]
    public async Task<ActionResult<TransactionDto>> Deposit([FromBody] DepositDto dto)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.DepositAsync(userId, dto);
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("withdraw")]
    public async Task<ActionResult<TransactionDto>> Withdraw([FromBody] WithdrawDto dto)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.WithdrawAsync(userId, dto);
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<TransactionDto>> Transfer([FromBody] TransferDto dto)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.TransferAsync(userId, dto);
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }
}
