using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly ApprovalService _approvalService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public ApprovalController(ApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("pending")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> GetPending([FromQuery] string? branchId)
    {
        var requests = await _approvalService.GetPendingAsync(branchId);
        return Ok(requests);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRequests()
    {
        var requests = await _approvalService.GetUserRequestsAsync(UserId);
        return Ok(requests);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalActionDto dto)
    {
        var request = await _approvalService.ApproveAsync(id, UserId, dto.Notes);
        return Ok(request);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApprovalActionDto dto)
    {
        var request = await _approvalService.RejectAsync(id, UserId, dto.Notes);
        return Ok(request);
    }
}

public class ApprovalActionDto
{
    public string Notes { get; set; } = string.Empty;
}
