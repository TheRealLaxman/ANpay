using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var stats = await _reportService.GetSuperAdminStatsAsync();
        return Ok(stats);
    }

    [HttpGet("transactions")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetTransactionReport([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? branchId)
    {
        var report = await _reportService.GetTransactionReportAsync(from, to, branchId);
        return Ok(report);
    }

    [HttpGet("branches")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetBranchReport()
    {
        var report = await _reportService.GetBranchReportAsync();
        return Ok(report);
    }
}
