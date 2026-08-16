using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InsuranceController : ControllerBase
{
    private readonly InsuranceService _insuranceService;
    private readonly ILogger<InsuranceController> _logger;

    public InsuranceController(InsuranceService insuranceService, ILogger<InsuranceController> logger)
    {
        _insuranceService = insuranceService;
        _logger = logger;
    }

    [HttpPost("purchase")]
    public async Task<ActionResult<Insurance>> PurchaseInsurance([FromBody] PurchaseInsuranceRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var insurance = await _insuranceService.PurchaseInsuranceAsync(
            userId, request.WalletId, request.Type, request.PlanName, request.PremiumAmount,
            request.CoverageAmount, request.Frequency, request.DurationMonths);

        return Ok(insurance);
    }

    [HttpGet]
    public async Task<ActionResult<List<Insurance>>> GetMyInsurances()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var insurances = await _insuranceService.GetUserInsurancesAsync(userId);
        return Ok(insurances);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Insurance>> GetInsurance(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var insurance = await _insuranceService.GetInsuranceByIdAsync(id, userId);
        if (insurance == null) return NotFound();
        return Ok(insurance);
    }

    [HttpPost("{id}/claim")]
    public async Task<ActionResult<InsuranceClaim>> FileClaim(Guid id, [FromBody] FileClaimRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var claim = await _insuranceService.FileClaimAsync(id, userId, request.Title, request.Description, request.Amount, request.Documents);
        return Ok(claim);
    }

    [HttpGet("{id}/claims")]
    public async Task<ActionResult<List<InsuranceClaim>>> GetClaims(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var claims = await _insuranceService.GetClaimsAsync(id, userId);
        return Ok(claims);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class PurchaseInsuranceRequest
{
    public Guid WalletId { get; set; }
    public InsuranceType Type { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PremiumAmount { get; set; }
    public decimal CoverageAmount { get; set; }
    public InsuranceFrequency Frequency { get; set; } = InsuranceFrequency.Monthly;
    public int DurationMonths { get; set; } = 12;
}

public class FileClaimRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Documents { get; set; }
}
