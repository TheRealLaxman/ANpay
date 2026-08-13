using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReconciliationController : ControllerBase
{
    private readonly ReconciliationService _reconciliationService;
    private readonly ILogger<ReconciliationController> _logger;

    public ReconciliationController(ReconciliationService reconciliationService, ILogger<ReconciliationController> logger)
    {
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    [HttpPost("run")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> RunReconciliation([FromBody] RunReconciliationDto dto)
    {
        var record = await _reconciliationService.RunReconciliationAsync(
            dto.Type, dto.PeriodStart, dto.PeriodEnd, dto.Source, dto.ExternalBalance);

        return Ok(new
        {
            success = true,
            data = record,
            message = record.IsMatched ? "Reconciliation matched" : "Discrepancy found"
        });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetRecords([FromQuery] ReconciliationStatus? status)
    {
        var records = await _reconciliationService.GetRecordsAsync(status);
        return Ok(new { success = true, data = records });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetRecordById(Guid id)
    {
        var record = await _reconciliationService.GetRecordByIdAsync(id);
        if (record == null) return NotFound(new { success = false, message = "Record not found" });
        return Ok(new { success = true, data = record });
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> ResolveRecord(Guid id, [FromBody] ResolveReconciliationDto dto)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await _reconciliationService.ResolveRecordAsync(id, dto.Notes, userId!);
        return Ok(new { success = true, message = "Reconciliation resolved" });
    }

    [HttpPost("{id}/escalate")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> EscalateRecord(Guid id, [FromBody] EscalateReconciliationDto dto)
    {
        await _reconciliationService.EscalateRecordAsync(id, dto.Notes);
        return Ok(new { success = true, message = "Reconciliation escalated" });
    }
}

public class RunReconciliationDto
{
    public ReconciliationType Type { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal ExternalBalance { get; set; }
}

public class ResolveReconciliationDto
{
    public string? Notes { get; set; }
}

public class EscalateReconciliationDto
{
    public string? Notes { get; set; }
}
