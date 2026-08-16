using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly PosService _posService;
    private readonly ILogger<PosController> _logger;

    public PosController(PosService posService, ILogger<PosController> logger)
    {
        _posService = posService;
        _logger = logger;
    }

    [HttpPost("devices")]
    public async Task<ActionResult<PosDevice>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var device = await _posService.RegisterDeviceAsync(
            request.MerchantId, request.DeviceSerial, request.DeviceModel,
            request.SupportsNfc, request.SupportsChip, request.SupportsSwipe, request.SupportsTapToPay);

        return Ok(device);
    }

    [HttpGet("devices/{merchantId}")]
    public async Task<ActionResult<List<PosDevice>>> GetMerchantDevices(Guid merchantId)
    {
        var devices = await _posService.GetMerchantDevicesAsync(merchantId);
        return Ok(devices);
    }

    [HttpPost("devices/{id}/activate")]
    public async Task<ActionResult<PosDevice>> ActivateDevice(Guid id, [FromQuery] Guid merchantId)
    {
        var device = await _posService.ActivateDeviceAsync(id, merchantId);
        return Ok(device);
    }

    [HttpPost("process-sale")]
    public async Task<ActionResult<PosTransaction>> ProcessSale([FromBody] ProcessSaleRequest request)
    {
        var transaction = await _posService.ProcessSaleAsync(
            request.DeviceId, request.WalletId, request.Amount, request.Description,
            request.IsTapToPay, request.CardLast4, request.CardType);

        return Ok(transaction);
    }

    [HttpGet("devices/{deviceId}/transactions")]
    public async Task<ActionResult<List<PosTransaction>>> GetDeviceTransactions(Guid deviceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var transactions = await _posService.GetDeviceTransactionsAsync(deviceId, page, pageSize);
        return Ok(transactions);
    }

    [HttpPost("transactions/{id}/refund")]
    public async Task<ActionResult<PosTransaction>> RefundTransaction(Guid id, [FromBody] RefundRequest? request = null)
    {
        var transaction = await _posService.RefundTransactionAsync(id, request?.Reason);
        return Ok(transaction);
    }
}

public class RegisterDeviceRequest
{
    public Guid MerchantId { get; set; }
    public string DeviceSerial { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public bool SupportsNfc { get; set; } = true;
    public bool SupportsChip { get; set; } = true;
    public bool SupportsSwipe { get; set; } = true;
    public bool SupportsTapToPay { get; set; } = false;
}

public class ProcessSaleRequest
{
    public Guid DeviceId { get; set; }
    public Guid? WalletId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public bool IsTapToPay { get; set; } = false;
    public string? CardLast4 { get; set; }
    public string? CardType { get; set; }
}

public class RefundRequest
{
    public string? Reason { get; set; }
}
