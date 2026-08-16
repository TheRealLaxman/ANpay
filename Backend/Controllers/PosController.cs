using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;
using ANpay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly PosService _posService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PosController> _logger;

    public PosController(PosService posService, ApplicationDbContext context, ILogger<PosController> logger)
    {
        _posService = posService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("devices")]
    public async Task<ActionResult<PosDevice>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify the user owns this merchant
        var merchant = await _context.Merchants.FindAsync(request.MerchantId);
        if (merchant == null) return NotFound(new { message = "Merchant not found" });
        if (merchant.UserId != userId) return Forbid();

        var device = await _posService.RegisterDeviceAsync(
            request.MerchantId, request.DeviceSerial, request.DeviceModel,
            request.SupportsNfc, request.SupportsChip, request.SupportsSwipe, request.SupportsTapToPay);

        return Ok(device);
    }

    [HttpGet("devices/{merchantId}")]
    public async Task<ActionResult<List<PosDevice>>> GetMerchantDevices(Guid merchantId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var merchant = await _context.Merchants.FindAsync(merchantId);
        if (merchant == null) return NotFound(new { message = "Merchant not found" });
        if (merchant.UserId != userId) return Forbid();

        var devices = await _posService.GetMerchantDevicesAsync(merchantId);
        return Ok(devices);
    }

    [HttpPost("devices/{id}/activate")]
    public async Task<ActionResult<PosDevice>> ActivateDevice(Guid id, [FromQuery] Guid merchantId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var merchant = await _context.Merchants.FindAsync(merchantId);
        if (merchant == null) return NotFound(new { message = "Merchant not found" });
        if (merchant.UserId != userId) return Forbid();

        var device = await _posService.ActivateDeviceAsync(id, merchantId);
        return Ok(device);
    }

    [HttpPost("process-sale")]
    public async Task<ActionResult<PosTransaction>> ProcessSale([FromBody] ProcessSaleRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify device exists and belongs to a merchant owned by this user
        var device = await _context.PosDevices.FindAsync(request.DeviceId);
        if (device == null) return NotFound(new { message = "POS device not found" });

        var deviceMerchant = await _context.Merchants.FindAsync(device.MerchantId);
        if (deviceMerchant == null || deviceMerchant.UserId != userId) return Forbid();

        // Verify wallet ownership if walletId is provided
        if (request.WalletId.HasValue)
        {
            var wallet = await _context.Wallets.FindAsync(request.WalletId.Value);
            if (wallet == null) return NotFound(new { message = "Wallet not found" });
            if (wallet.UserId != userId) return Forbid();
        }

        var transaction = await _posService.ProcessSaleAsync(
            request.DeviceId, request.WalletId, request.Amount, request.Description,
            request.IsTapToPay, request.CardLast4, request.CardType);

        return Ok(transaction);
    }

    [HttpGet("devices/{deviceId}/transactions")]
    public async Task<ActionResult<List<PosTransaction>>> GetDeviceTransactions(Guid deviceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify user owns the device's merchant
        var device = await _context.PosDevices.FindAsync(deviceId);
        if (device == null) return NotFound(new { message = "POS device not found" });

        var merchant = await _context.Merchants.FindAsync(device.MerchantId);
        if (merchant == null || merchant.UserId != userId) return Forbid();

        var transactions = await _posService.GetDeviceTransactionsAsync(deviceId, page, pageSize);
        return Ok(transactions);
    }

    [HttpPost("transactions/{id}/refund")]
    public async Task<ActionResult<PosTransaction>> RefundTransaction(Guid id, [FromBody] RefundRequest? request = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify user owns the POS transaction via device → merchant → user chain
        var posTx = await _context.PosTransactions.FindAsync(id);
        if (posTx == null) return NotFound(new { message = "POS transaction not found" });

        var device = await _context.PosDevices.FindAsync(posTx.PosDeviceId);
        if (device == null) return Forbid();

        var merchant = await _context.Merchants.FindAsync(device.MerchantId);
        if (merchant == null || merchant.UserId != userId) return Forbid();

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
