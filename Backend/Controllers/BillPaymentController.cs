using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillPaymentController : ControllerBase
{
    private readonly BillPaymentService _billPaymentService;
    private readonly ILogger<BillPaymentController> _logger;

    public BillPaymentController(BillPaymentService billPaymentService, ILogger<BillPaymentController> logger)
    {
        _billPaymentService = billPaymentService;
        _logger = logger;
    }

    [HttpGet("providers")]
    public async Task<ActionResult<List<BillProvider>>> GetProviders([FromQuery] BillCategory? category = null)
    {
        var providers = await _billPaymentService.GetProvidersAsync(category);
        return Ok(providers);
    }

    [HttpGet("providers/{code}")]
    public async Task<ActionResult<BillProvider>> GetProvider(string code)
    {
        var provider = await _billPaymentService.GetProviderByCodeAsync(code);
        if (provider == null) return NotFound();
        return Ok(provider);
    }

    [HttpPost("pay")]
    public async Task<ActionResult<BillPayment>> PayBill([FromBody] PayBillRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var payment = await _billPaymentService.PayBillAsync(
            userId, request.WalletId, request.ProviderCode, request.BillerCode, request.CustomerReference, request.Amount, request.Description);

        return Ok(payment);
    }

    [HttpGet]
    public async Task<ActionResult<List<BillPayment>>> GetMyPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var payments = await _billPaymentService.GetUserBillPaymentsAsync(userId, page, pageSize);
        return Ok(payments);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BillPayment>> GetPayment(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var payment = await _billPaymentService.GetBillPaymentByIdAsync(id, userId);
        if (payment == null) return NotFound();
        return Ok(payment);
    }

    [HttpPost("providers")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<BillProvider>> CreateProvider([FromBody] CreateProviderRequest request)
    {
        var provider = await _billPaymentService.CreateProviderAsync(
            request.Name, request.Category, request.Code, request.MinimumAmount, request.MaximumAmount, request.FixedFee, request.PercentageFee, request.Currency);
        return CreatedAtAction(nameof(GetProvider), new { code = provider.Code }, provider);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class PayBillRequest
{
    public Guid WalletId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string BillerCode { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class CreateProviderRequest
{
    public string Name { get; set; } = string.Empty;
    public BillCategory Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal MinimumAmount { get; set; }
    public decimal MaximumAmount { get; set; }
    public decimal FixedFee { get; set; }
    public decimal PercentageFee { get; set; }
    public string Currency { get; set; } = "NGN";
}
