using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemittanceController : ControllerBase
{
    private readonly RemittanceService _remittanceService;
    private readonly ILogger<RemittanceController> _logger;

    public RemittanceController(RemittanceService remittanceService, ILogger<RemittanceController> logger)
    {
        _remittanceService = remittanceService;
        _logger = logger;
    }

    [HttpGet("partners")]
    public async Task<ActionResult<List<RemittancePartner>>> GetPartners([FromQuery] string? country = null)
    {
        var partners = await _remittanceService.GetPartnersAsync(country);
        return Ok(partners);
    }

    [HttpGet("countries")]
    public async Task<ActionResult<List<string>>> GetSupportedCountries()
    {
        var countries = await _remittanceService.GetSupportedCountriesAsync();
        return Ok(countries);
    }

    [HttpPost("send")]
    public async Task<ActionResult<Remittance>> SendMoney([FromBody] SendMoneyRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var remittance = await _remittanceService.SendMoneyAsync(
            userId, request.WalletId, request.RecipientName, request.RecipientCountry,
            request.RecipientBankCode, request.RecipientAccountNumber, request.RecipientCurrency,
            request.SendAmount, request.RecipientBankName, request.Purpose, request.PurposeDescription);

        return Ok(remittance);
    }

    [HttpGet]
    public async Task<ActionResult<List<Remittance>>> GetMyRemittances([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var remittances = await _remittanceService.GetUserRemittancesAsync(userId, page, pageSize);
        return Ok(remittances);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Remittance>> GetRemittance(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var remittance = await _remittanceService.GetRemittanceByIdAsync(id, userId);
        if (remittance == null) return NotFound();
        return Ok(remittance);
    }

    [HttpGet("track/{trackingNumber}")]
    public async Task<ActionResult<Remittance>> TrackRemittance(string trackingNumber)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var remittance = await _remittanceService.GetRemittanceByTrackingAsync(trackingNumber);
        if (remittance == null) return NotFound();
        if (remittance.SenderUserId != userId && !User.IsInRole("SuperAdmin"))
            return Forbid();
        return Ok(remittance);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class SendMoneyRequest
{
    public Guid WalletId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientCountry { get; set; } = string.Empty;
    public string RecipientBankCode { get; set; } = string.Empty;
    public string RecipientAccountNumber { get; set; } = string.Empty;
    public string RecipientCurrency { get; set; } = "USD";
    public string? RecipientBankName { get; set; }
    public decimal SendAmount { get; set; }
    public RemittancePurpose Purpose { get; set; } = RemittancePurpose.FamilySupport;
    public string? PurposeDescription { get; set; }
}
