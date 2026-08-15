using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly FeeService _feeService;
    private readonly LimitService _limitService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WalletController> _logger;

    public WalletController(WalletService walletService, FeeService feeService, LimitService limitService, UserManager<ApplicationUser> userManager, ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _feeService = feeService;
        _limitService = limitService;
        _userManager = userManager;
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

    [HttpPost("fee-quote")]
    public async Task<ActionResult<FeeQuoteResultDto>> GetFeeQuote([FromBody] FeeQuoteDto dto)
    {
        var appliesTo = dto.Type.ToLower() switch
        {
            "deposit" => Models.FeeAppliesTo.Deposit,
            "withdrawal" => Models.FeeAppliesTo.Withdrawal,
            "transfer" => Models.FeeAppliesTo.Transfer,
            _ => Models.FeeAppliesTo.Transfer
        };
        var fee = await _feeService.CalculateFeeAsync(appliesTo, dto.Amount, dto.Currency);
        return Ok(new FeeQuoteResultDto
        {
            Amount = dto.Amount,
            Fee = fee,
            Total = dto.Amount + fee,
            Currency = dto.Currency
        });
    }

    [HttpGet("limits")]
    public async Task<ActionResult<TransactionLimitsDto>> GetMyLimits()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var allLimits = await _limitService.GetAllAsync();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var role = ((Models.AppUserRole)(int)user.Role).ToString();
        var myLimits = allLimits.Where(l => l.RoleName == role && l.Currency == "NGN").ToList();

        return Ok(new TransactionLimitsDto
        {
            Limits = myLimits.Select(l => new LimitItem
            {
                Type = l.LimitType.ToString(),
                LimitAmount = l.LimitAmount,
                Used = 0,
                Remaining = l.LimitAmount,
                Currency = l.Currency
            }).ToList()
        });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
