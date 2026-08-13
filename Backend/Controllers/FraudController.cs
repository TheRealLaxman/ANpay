using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/fraud")]
public class FraudController : ControllerBase
{
    private readonly FraudService _fraudService;
    private readonly ILogger<FraudController> _logger;

    public FraudController(FraudService fraudService, ILogger<FraudController> logger)
    {
        _fraudService = fraudService;
        _logger = logger;
    }

    [HttpGet("alerts")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetOpenAlerts()
    {
        var alerts = await _fraudService.GetOpenAlertsAsync();
        return Ok(alerts);
    }

    [HttpGet("alerts/{id}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetAlert(Guid id)
    {
        var alert = await _fraudService.GetAlertByIdAsync(id);
        if (alert == null) return NotFound();
        return Ok(alert);
    }

    [HttpPost("alerts/{id}/status")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> UpdateAlertStatus(Guid id, [FromBody] UpdateFraudAlertStatusDto dto)
    {
        await _fraudService.UpdateAlertStatusAsync(id, dto.Status, dto.Resolution);
        return Ok(new { message = "Alert status updated" });
    }

    [HttpPost("alerts/{id}/assign")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> AssignAlert(Guid id, [FromBody] AssignFraudAlertDto dto)
    {
        await _fraudService.AssignAlertAsync(id, dto.AssignedToId);
        return Ok(new { message = "Alert assigned" });
    }

    [HttpGet("risk-score/{userId}")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetRiskScore(string userId, [FromQuery] decimal amount,
        [FromQuery] string ipAddress, [FromQuery] string deviceInfo)
    {
        var score = await _fraudService.CalculateRiskScoreAsync(userId, amount, ipAddress, deviceInfo);
        return Ok(new { userId, score, level = score switch
        {
            >= 70 => "Critical",
            >= 40 => "High",
            >= 20 => "Medium",
            _ => "Low"
        }});
    }
}

public class UpdateFraudAlertStatusDto
{
    public FraudAlertStatus Status { get; set; }
    public string? Resolution { get; set; }
}

public class AssignFraudAlertDto
{
    public string AssignedToId { get; set; } = string.Empty;
}
