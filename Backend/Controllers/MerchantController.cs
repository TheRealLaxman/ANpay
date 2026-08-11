using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MerchantController : ControllerBase
{
    private readonly MerchantService _merchantService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public MerchantController(MerchantService merchantService)
    {
        _merchantService = merchantService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] MerchantRegisterDto dto)
    {
        var merchant = await _merchantService.RegisterAsync(UserId, dto.BusinessName, dto.BusinessType,
            dto.Address, dto.Phone, dto.Email, dto.TaxId);
        return Ok(merchant);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyMerchant()
    {
        var merchant = await _merchantService.GetByUserIdAsync(UserId);
        return Ok(merchant);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var merchant = await _merchantService.GetByIdAsync(id);
        return Ok(merchant);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetAll()
    {
        var merchants = await _merchantService.GetAllAsync();
        return Ok(merchants);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var merchant = await _merchantService.ApproveAsync(id);
        return Ok(merchant);
    }

    [HttpPost("{id}/suspend")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var merchant = await _merchantService.SuspendAsync(id);
        return Ok(merchant);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var merchant = await _merchantService.GetByUserIdAsync(UserId);
        if (merchant == null) return NotFound("No merchant account found");
        var dashboard = await _merchantService.GetDashboardAsync(merchant.Id);
        return Ok(dashboard);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment([FromBody] MerchantPaymentDto dto)
    {
        var merchant = await _merchantService.GetByUserIdAsync(UserId);
        if (merchant == null) return NotFound("No merchant account found");
        var payment = await _merchantService.CreatePaymentAsync(merchant.Id, dto.Amount, dto.Description, dto.OrderReference, dto.CustomerId);
        return Ok(payment);
    }

    [HttpPost("payments/{id}/complete")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,Official")]
    public async Task<IActionResult> CompletePayment(Guid id, [FromBody] CompletePaymentDto dto)
    {
        var payment = await _merchantService.CompletePaymentAsync(id, dto.PaymentReference);
        return Ok(payment);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments()
    {
        var merchant = await _merchantService.GetByUserIdAsync(UserId);
        if (merchant == null) return NotFound("No merchant account found");
        var payments = await _merchantService.GetMerchantPaymentsAsync(merchant.Id);
        return Ok(payments);
    }

    [HttpPost("settlements")]
    public async Task<IActionResult> CreateSettlement([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var merchant = await _merchantService.GetByUserIdAsync(UserId);
        if (merchant == null) return NotFound("No merchant account found");
        var settlement = await _merchantService.CreateSettlementAsync(merchant.Id, from, to);
        return Ok(settlement);
    }
}

public class MerchantRegisterDto
{
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
}

public class MerchantPaymentDto
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
}

public class CompletePaymentDto
{
    public string PaymentReference { get; set; } = string.Empty;
}
