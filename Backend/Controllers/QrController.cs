using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QrController : ControllerBase
{
    private readonly QrPaymentService _qrService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public QrController(QrPaymentService qrService)
    {
        _qrService = qrService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateQr([FromBody] GenerateQrDto dto)
    {
        var qr = await _qrService.GenerateQrCodeAsync(UserId, dto.Type, dto.FixedAmount,
            dto.WalletId, dto.MerchantId, dto.Description, dto.UsageLimit, dto.ExpiresInMinutes);
        return Ok(qr);
    }

    [HttpPost("scan")]
    public async Task<IActionResult> ScanQr([FromBody] ScanQrDto dto)
    {
        var result = await _qrService.ScanQrCodeAsync(dto.Code, UserId);
        return Ok(result);
    }

    [HttpPost("{qrCodeId}/pay")]
    public async Task<IActionResult> ProcessPayment(Guid qrCodeId, [FromBody] QrPayDto dto)
    {
        await _qrService.ProcessQrPaymentAsync(qrCodeId, UserId, dto.Amount, dto.SourceWalletId);
        return Ok(new { message = "Payment processed" });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyQrCodes()
    {
        var codes = await _qrService.GetUserQrCodesAsync(UserId);
        return Ok(codes);
    }

    [HttpPost("payment-links")]
    public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePaymentLinkDto dto)
    {
        var link = await _qrService.CreatePaymentLinkAsync(UserId, dto.Title, dto.Description,
            dto.FixedAmount, dto.Currency, dto.MerchantId, dto.ExpiresInDays);
        return Ok(link);
    }

    [HttpGet("payment-links/{linkId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaymentLink(string linkId)
    {
        var link = await _qrService.GetPaymentLinkAsync(linkId);
        if (link == null) return NotFound();
        return Ok(link);
    }
}

public class GenerateQrDto
{
    public QrCodeType Type { get; set; }
    public decimal? FixedAmount { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? MerchantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int UsageLimit { get; set; } = 1;
    public int ExpiresInMinutes { get; set; } = 30;
}

public class ScanQrDto
{
    public string Code { get; set; } = string.Empty;
}

public class QrPayDto
{
    public decimal Amount { get; set; }
    public Guid? SourceWalletId { get; set; }
}

public class CreatePaymentLinkDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public string Currency { get; set; } = "NGN";
    public Guid? MerchantId { get; set; }
    public int ExpiresInDays { get; set; } = 7;
}
