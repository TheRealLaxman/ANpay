using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly LoyaltyService _loyaltyService;
    private readonly ILogger<LoyaltyController> _logger;

    public LoyaltyController(LoyaltyService loyaltyService, ILogger<LoyaltyController> logger)
    {
        _loyaltyService = loyaltyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<LoyaltyPoint>> GetMyPoints()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var points = await _loyaltyService.GetOrCreateLoyaltyPointsAsync(userId);
        return Ok(points);
    }

    [HttpPost("earn")]
    public async Task<ActionResult<LoyaltyPoint>> EarnPoints([FromBody] EarnPointsRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var points = await _loyaltyService.EarnPointsAsync(userId, request.Points, request.Type, request.Description, request.TransactionId, request.WalletId);
        return Ok(points);
    }

    [HttpPost("redeem")]
    public async Task<ActionResult<LoyaltyPoint>> RedeemPoints([FromBody] RedeemPointsRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var points = await _loyaltyService.RedeemPointsAsync(userId, request.Points, request.Description);
        return Ok(points);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<LoyaltyTransaction>>> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var transactions = await _loyaltyService.GetTransactionHistoryAsync(userId, page, pageSize);
        return Ok(transactions);
    }

    [HttpPost("referral")]
    public async Task<ActionResult<Referral>> ProcessReferral([FromBody] ReferralRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var referral = await _loyaltyService.ProcessReferralAsync(userId, request.ReferredUserId);
        return Ok(referral);
    }

    [HttpGet("referrals")]
    public async Task<ActionResult<List<Referral>>> GetMyReferrals()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var referrals = await _loyaltyService.GetUserReferralsAsync(userId);
        return Ok(referrals);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class EarnPointsRequest
{
    public int Points { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public Guid? WalletId { get; set; }
}

public class RedeemPointsRequest
{
    public int Points { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ReferralRequest
{
    public string ReferredUserId { get; set; } = string.Empty;
}
