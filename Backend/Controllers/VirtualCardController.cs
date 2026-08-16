using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VirtualCardController : ControllerBase
{
    private readonly VirtualCardService _virtualCardService;
    private readonly ILogger<VirtualCardController> _logger;

    public VirtualCardController(VirtualCardService virtualCardService, ILogger<VirtualCardController> logger)
    {
        _virtualCardService = virtualCardService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<VirtualCard>> CreateCard([FromBody] CreateCardRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.CreateCardAsync(
            userId, request.WalletId, request.CardType, request.CardHolderName, request.DailyLimit, request.MonthlyLimit);

        return CreatedAtAction(nameof(GetCard), new { id = card.Id }, card);
    }

    [HttpGet]
    public async Task<ActionResult<List<VirtualCard>>> GetMyCards()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var cards = await _virtualCardService.GetUserCardsAsync(userId);
        return Ok(cards);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VirtualCard>> GetCard(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.GetCardByIdAsync(id, userId);
        if (card == null) return NotFound();
        return Ok(card);
    }

    [HttpPost("{id}/freeze")]
    public async Task<ActionResult<VirtualCard>> FreezeCard(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.FreezeCardAsync(id, userId);
        return Ok(card);
    }

    [HttpPost("{id}/unfreeze")]
    public async Task<ActionResult<VirtualCard>> UnfreezeCard(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.UnfreezeCardAsync(id, userId);
        return Ok(card);
    }

    [HttpPut("{id}/limits")]
    public async Task<ActionResult<VirtualCard>> UpdateLimits(Guid id, [FromBody] UpdateLimitsRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.UpdateCardLimitsAsync(id, userId, request.DailyLimit, request.MonthlyLimit);
        return Ok(card);
    }

    [HttpPost("{id}/toggle-online")]
    public async Task<ActionResult<VirtualCard>> ToggleOnlinePayments(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.ToggleOnlinePaymentsAsync(id, userId);
        return Ok(card);
    }

    [HttpPost("{id}/toggle-atm")]
    public async Task<ActionResult<VirtualCard>> ToggleAtmWithdrawals(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.ToggleAtmWithdrawalsAsync(id, userId);
        return Ok(card);
    }

    [HttpPost("{id}/close")]
    public async Task<ActionResult<VirtualCard>> CloseCard(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var card = await _virtualCardService.CloseCardAsync(id, userId);
        return Ok(card);
    }

    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<List<VirtualCardTransaction>>> GetCardTransactions(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var transactions = await _virtualCardService.GetCardTransactionsAsync(id, userId, page, pageSize);
        return Ok(transactions);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class CreateCardRequest
{
    public Guid WalletId { get; set; }
    public VirtualCardType CardType { get; set; } = VirtualCardType.Standard;
    public string CardHolderName { get; set; } = string.Empty;
    public decimal DailyLimit { get; set; } = 1000;
    public decimal MonthlyLimit { get; set; } = 10000;
}

public class UpdateLimitsRequest
{
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
}
