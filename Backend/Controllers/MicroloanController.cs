using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MicroloanController : ControllerBase
{
    private readonly MicroloanService _microloanService;
    private readonly ILogger<MicroloanController> _logger;

    public MicroloanController(MicroloanService microloanService, ILogger<MicroloanController> logger)
    {
        _microloanService = microloanService;
        _logger = logger;
    }

    [HttpPost("apply")]
    public async Task<ActionResult<Microloan>> ApplyForLoan([FromBody] ApplyLoanRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var loan = await _microloanService.ApplyForLoanAsync(
            userId, request.WalletId, request.Amount, request.TenureDays, request.Purpose, request.PurposeDescription);

        return Ok(loan);
    }

    [HttpGet]
    public async Task<ActionResult<List<Microloan>>> GetMyLoans()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var loans = await _microloanService.GetUserLoansAsync(userId);
        return Ok(loans);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Microloan>> GetLoan(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var loan = await _microloanService.GetLoanByIdAsync(id, userId);
        if (loan == null) return NotFound();
        return Ok(loan);
    }

    [HttpPost("{id}/repay")]
    public async Task<ActionResult<MicroloanRepayment>> MakeRepayment(Guid id, [FromBody] RepayLoanRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var repayment = await _microloanService.MakeRepaymentAsync(id, request.WalletId, userId);
        return Ok(repayment);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "SuperAdmin,BranchAdmin")]
    public async Task<ActionResult<Microloan>> ApproveLoan(Guid id)
    {
        var loan = await _microloanService.ApproveLoanAsync(id);
        return Ok(loan);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class ApplyLoanRequest
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public int TenureDays { get; set; } = 30;
    public MicroloanPurpose Purpose { get; set; } = MicroloanPurpose.Personal;
    public string? PurposeDescription { get; set; }
}

public class RepayLoanRequest
{
    public Guid WalletId { get; set; }
}
