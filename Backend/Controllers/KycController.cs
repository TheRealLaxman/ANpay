using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KycController : ControllerBase
{
    private readonly KycService _kycService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public KycController(KycService kycService)
    {
        _kycService = kycService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyKyc()
    {
        var profile = await _kycService.GetByUserIdAsync(UserId);
        return Ok(profile);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] KycSubmitDto dto)
    {
        var profile = await _kycService.SubmitAsync(UserId, dto);
        return Ok(profile);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetPending()
    {
        var profiles = await _kycService.GetPendingAsync();
        return Ok(profiles);
    }

    [HttpPost("{profileId}/review")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Review(Guid profileId, [FromBody] KycReviewDto dto)
    {
        var profile = await _kycService.ReviewAsync(profileId, dto.Approve, dto.Notes);
        return Ok(profile);
    }
}

public class KycReviewDto
{
    public bool Approve { get; set; }
    public string Notes { get; set; } = string.Empty;
}
