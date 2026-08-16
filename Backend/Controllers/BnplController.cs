using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BnplController : ControllerBase
{
    private readonly BnplService _bnplService;
    private readonly ILogger<BnplController> _logger;

    public BnplController(BnplService bnplService, ILogger<BnplController> logger)
    {
        _bnplService = bnplService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<BuyNowPayLater>> CreateBnpl([FromBody] CreateBnplRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bnpl = await _bnplService.CreateBnplAsync(
            userId, request.WalletId, request.TotalAmount, request.Installments,
            request.Frequency, request.MerchantId, request.MerchantPaymentId,
            request.DownPayment, request.InterestRate);

        return Ok(bnpl);
    }

    [HttpGet]
    public async Task<ActionResult<List<BuyNowPayLater>>> GetMyBnpls()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bnpls = await _bnplService.GetUserBnplsAsync(userId);
        return Ok(bnpls);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BuyNowPayLater>> GetBnpl(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bnpl = await _bnplService.GetBnplByIdAsync(id, userId);
        if (bnpl == null) return NotFound();
        return Ok(bnpl);
    }

    [HttpPost("{id}/pay-installment")]
    public async Task<ActionResult<BnplInstallment>> PayInstallment(Guid id, [FromBody] PayInstallmentRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var installment = await _bnplService.PayInstallmentAsync(id, request.WalletId, userId);
        return Ok(installment);
    }

    [HttpPost("{id}/pause")]
    public async Task<ActionResult<BuyNowPayLater>> PauseBnpl(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bnpl = await _bnplService.PauseBnplAsync(id, userId);
        return Ok(bnpl);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class CreateBnplRequest
{
    public Guid WalletId { get; set; }
    public decimal TotalAmount { get; set; }
    public int Installments { get; set; } = 4;
    public BnplFrequency Frequency { get; set; } = BnplFrequency.Weekly;
    public Guid? MerchantId { get; set; }
    public Guid? MerchantPaymentId { get; set; }
    public decimal DownPayment { get; set; } = 0;
    public decimal InterestRate { get; set; } = 0;
}

public class PayInstallmentRequest
{
    public Guid WalletId { get; set; }
}
