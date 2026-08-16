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

    [HttpGet("revenue")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetRevenueReport([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var report = await _reportService.GetRevenueReportAsync(from, to);
        return Ok(report);
    }

    [HttpGet("branches/compare")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetBranchComparison()
    {
        var report = await _reportService.GetBranchComparisonReportAsync();
        return Ok(report);
    }

    [HttpGet("customer-statement")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetCustomerStatement([FromQuery] string userId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId is required.");

        var report = await _reportService.GetCustomerStatementAsync(userId, from, to);
        return Ok(report);
    }

    [HttpGet("transactions/export")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> ExportTransactionsCsv([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? branchId)
    {
        var report = await _reportService.GetTransactionReportAsync(from, to, branchId);
        var csv = GenerateTransactionsCsv(report);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"transactions_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    [HttpGet("revenue/export")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ExportRevenueCsv([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var report = await _reportService.GetRevenueReportAsync(from, to);
        var csv = GenerateRevenueCsv(report);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"revenue_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    [HttpGet("branches/compare/export")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ExportBranchComparisonCsv()
    {
        var report = await _reportService.GetBranchComparisonReportAsync();
        var csv = GenerateBranchComparisonCsv(report);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"branch_comparison_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("customer-statement/export")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ExportCustomerStatementCsv([FromQuery] string userId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId is required.");

        var report = await _reportService.GetCustomerStatementAsync(userId, from, to);
        var csv = GenerateTransactionsCsv(report);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"statement_{userId}_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    private static string GenerateTransactionsCsv(List<Services.TransactionReportDto> transactions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("TransactionId,Type,Amount,Currency,Status,UserEmail,UserName,BranchId,CreatedAt");
        foreach (var t in transactions)
        {
            sb.AppendLine($"{t.TransactionId},{t.Type},{t.Amount},{t.Currency},{t.Status},\"{SanitizeCsvField(t.UserEmail)}\",\"{SanitizeCsvField(t.UserName)}\",{t.BranchId},{t.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
        return sb.ToString();
    }

    private static string GenerateRevenueCsv(Services.RevenueReportDto report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("PeriodFrom,PeriodTo,TotalFeesCollected,ExchangeRevenue,TotalTransactions,AverageTransactionAmount,TotalVolume");
        sb.AppendLine($"{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd},{report.TotalFeesCollected},{report.ExchangeRevenue},{report.TotalTransactions},{report.AverageTransactionAmount:F2},{report.TotalVolume}");
        return sb.ToString();
    }

    private static string GenerateBranchComparisonCsv(List<Services.BranchComparisonReportDto> branches)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BranchId,BranchName,Status,EmployeeCount,TotalTransactions,TodayTransactions,TotalVolume,TodayVolume,WalletCount,TotalBalance");
        foreach (var b in branches)
        {
            sb.AppendLine($"{b.BranchId},\"{SanitizeCsvField(b.BranchName)}\",{b.Status},{b.EmployeeCount},{b.TotalTransactions},{b.TodayTransactions},{b.TotalVolume},{b.TodayVolume},{b.WalletCount},{b.TotalBalance}");
        }
        return sb.ToString();
    }

    private static string SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // Prevent CSV injection by prefixing dangerous characters
        if (value.Length > 0 && (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@' || value[0] == '\t' || value[0] == '\r'))
            return "'" + value.Replace("\"", "\"\"");
        return value.Replace("\"", "\"\"");
    }
}
