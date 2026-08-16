using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditScoreController : ControllerBase
{
    private readonly CreditScoreService _creditScoreService;
    private readonly ILogger<CreditScoreController> _logger;

    public CreditScoreController(CreditScoreService creditScoreService, ILogger<CreditScoreController> logger)
    {
        _creditScoreService = creditScoreService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<CreditScore>> GetMyCreditScore()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var creditScore = await _creditScoreService.GetOrCreateCreditScoreAsync(userId);
        return Ok(creditScore);
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<CreditScore>> CalculateCreditScore()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var creditScore = await _creditScoreService.CalculateCreditScoreAsync(userId);
        return Ok(creditScore);
    }

    [HttpGet("factors")]
    public async Task<ActionResult<List<CreditScoreFactor>>> GetCreditScoreFactors()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var factors = await _creditScoreService.GetCreditScoreFactorsAsync(userId);
        return Ok(factors);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
