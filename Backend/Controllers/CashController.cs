using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashController : ControllerBase
{
    private readonly CashService _cashService;

    public CashController(CashService cashService)
    {
        _cashService = cashService;
    }

    [HttpGet("branch/{branchId}/today")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin,Official")]
    public async Task<IActionResult> GetTodayBalance(Guid branchId, [FromQuery] string? employeeId)
    {
        var balance = await _cashService.GetTodayBalanceAsync(branchId, employeeId);
        return Ok(balance);
    }

    [HttpPost("branch/{branchId}/adjust")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Adjust(Guid branchId, [FromBody] AdjustCashDto dto)
    {
        var balance = await _cashService.AdjustCashAsync(branchId, dto.Amount, dto.EmployeeId, dto.Reason);
        return Ok(balance);
    }

    [HttpPost("branch/{branchId}/close")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> RecordClosing(Guid branchId, [FromBody] ClosingDto dto)
    {
        var balance = await _cashService.RecordClosingAsync(branchId, dto.ActualClosing, dto.EmployeeId);
        return Ok(balance);
    }

    [HttpGet("branch/{branchId}/history")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetHistory(Guid branchId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var history = await _cashService.GetHistoryAsync(branchId, from, to);
        return Ok(history);
    }

    [HttpPost("{id}/reconcile")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Reconcile(Guid id)
    {
        await _cashService.ReconcileAsync(id);
        return Ok(new { message = "Reconciled successfully" });
    }
}

public class AdjustCashDto
{
    public decimal Amount { get; set; }
    public string? EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ClosingDto
{
    public decimal ActualClosing { get; set; }
    public string? EmployeeId { get; set; }
}
