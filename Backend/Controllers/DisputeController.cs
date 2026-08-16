using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/dispute")]
public class DisputeController : ControllerBase
{
    private readonly DisputeService _disputeService;
    private readonly ILogger<DisputeController> _logger;

    public DisputeController(DisputeService disputeService, ILogger<DisputeController> logger)
    {
        _disputeService = disputeService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDisputeDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var dispute = await _disputeService.CreateDisputeAsync(userId, dto);
        return CreatedAtAction(nameof(GetDispute), new { id = dispute.Id }, dispute);
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyDisputes()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var disputes = await _disputeService.GetUserDisputesAsync(userId);
        return Ok(disputes);
    }

    [HttpGet("open")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetOpenDisputes()
    {
        var disputes = await _disputeService.GetOpenDisputesAsync();
        return Ok(disputes);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetDispute(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("MainBranchAdmin") || User.IsInRole("BranchAdmin");

        var dispute = await _disputeService.GetDisputeByIdAsync(id, isAdmin ? null : userId);
        if (dispute == null) return NotFound();
        return Ok(dispute);
    }

    [HttpPost("{id}/messages")]
    [Authorize]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddDisputeMessageDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var message = await _disputeService.AddMessageAsync(id, userId, dto.Content, dto.IsInternal);
        return Ok(message);
    }

    [HttpPost("{id}/status")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDisputeStatusDto dto)
    {
        await _disputeService.UpdateStatusAsync(id, dto.Status, dto.Resolution);
        return Ok(new { message = "Status updated" });
    }

    [HttpPost("{id}/assign")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> AssignDispute(Guid id, [FromBody] AssignDisputeDto dto)
    {
        await _disputeService.AssignDisputeAsync(id, dto.AssignedToId);
        return Ok(new { message = "Dispute assigned" });
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> ResolveDispute(Guid id, [FromBody] ResolveDisputeDto dto)
    {
        await _disputeService.ResolveDisputeAsync(id, dto.Approve, dto.RefundAmount, dto.Resolution);
        return Ok(new { message = dto.Approve ? "Dispute resolved" : "Dispute rejected" });
    }
}

public class AddDisputeMessageDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class UpdateDisputeStatusDto
{
    public DisputeStatus Status { get; set; }
    public string? Resolution { get; set; }
}

public class AssignDisputeDto
{
    public string AssignedToId { get; set; } = string.Empty;
}

public class ResolveDisputeDto
{
    public bool Approve { get; set; }
    public decimal? RefundAmount { get; set; }
    public string Resolution { get; set; } = string.Empty;
}
