using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduledTransferController : ControllerBase
{
    private readonly ScheduledTransferService _scheduledTransferService;
    private readonly ILogger<ScheduledTransferController> _logger;

    public ScheduledTransferController(ScheduledTransferService scheduledTransferService, ILogger<ScheduledTransferController> logger)
    {
        _scheduledTransferService = scheduledTransferService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ScheduledTransferDto>>> GetMyScheduledTransfers()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transfers = await _scheduledTransferService.GetUserScheduledTransfersAsync(userId);
        return Ok(transfers);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduledTransferDto>> Create([FromBody] CreateScheduledTransferDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transfer = await _scheduledTransferService.CreateScheduledTransferAsync(userId, dto);
        return Ok(transfer);
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _scheduledTransferService.PauseScheduledTransferAsync(id, userId);
        return Ok(new { message = "Paused" });
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _scheduledTransferService.ResumeScheduledTransferAsync(id, userId);
        return Ok(new { message = "Resumed" });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _scheduledTransferService.CancelScheduledTransferAsync(id, userId);
        return Ok(new { message = "Cancelled" });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
