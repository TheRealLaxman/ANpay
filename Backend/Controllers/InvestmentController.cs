using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvestmentController : ControllerBase
{
    private readonly InvestmentService _investmentService;
    private readonly ILogger<InvestmentController> _logger;

    public InvestmentController(InvestmentService investmentService, ILogger<InvestmentController> logger)
    {
        _investmentService = investmentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Investment>> CreateInvestment([FromBody] CreateInvestmentRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var investment = await _investmentService.CreateInvestmentAsync(
            userId, request.WalletId, request.Type, request.ProductName, request.Amount,
            request.TenureDays, request.InterestRate, request.AutoRenew);

        return Ok(investment);
    }

    [HttpGet]
    public async Task<ActionResult<List<Investment>>> GetMyInvestments()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var investments = await _investmentService.GetUserInvestmentsAsync(userId);
        return Ok(investments);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Investment>> GetInvestment(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var investment = await _investmentService.GetInvestmentByIdAsync(id, userId);
        if (investment == null) return NotFound();
        return Ok(investment);
    }

    [HttpPost("{id}/withdraw")]
    public async Task<ActionResult<Investment>> WithdrawInvestment(Guid id, [FromBody] WithdrawInvestmentRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var investment = await _investmentService.WithdrawInvestmentAsync(id, request.WalletId, userId);
        return Ok(investment);
    }

    [HttpPost("savings-goal")]
    public async Task<ActionResult<SavingsGoal>> CreateSavingsGoal([FromBody] CreateSavingsGoalRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var goal = await _investmentService.CreateSavingsGoalAsync(
            userId, request.WalletId, request.Name, request.TargetAmount, request.TargetDate,
            request.Description, request.AutoSave, request.AutoSaveAmount, request.AutoSaveFrequency);

        return Ok(goal);
    }

    [HttpGet("savings-goals")]
    public async Task<ActionResult<List<SavingsGoal>>> GetMySavingsGoals()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var goals = await _investmentService.GetUserSavingsGoalsAsync(userId);
        return Ok(goals);
    }

    [HttpPost("savings-goal/{id}/contribute")]
    public async Task<ActionResult<SavingsGoal>> ContributeToGoal(Guid id, [FromBody] ContributeToGoalRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var goal = await _investmentService.ContributeToGoalAsync(id, request.WalletId, userId, request.Amount);
        return Ok(goal);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class CreateInvestmentRequest
{
    public Guid WalletId { get; set; }
    public InvestmentType Type { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TenureDays { get; set; } = 90;
    public decimal InterestRate { get; set; } = 10;
    public bool AutoRenew { get; set; } = false;
}

public class WithdrawInvestmentRequest
{
    public Guid WalletId { get; set; }
}

public class CreateSavingsGoalRequest
{
    public Guid WalletId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public DateTime TargetDate { get; set; }
    public string? Description { get; set; }
    public bool AutoSave { get; set; } = false;
    public decimal AutoSaveAmount { get; set; } = 0;
    public SavingsGoalFrequency AutoSaveFrequency { get; set; } = SavingsGoalFrequency.Weekly;
}

public class ContributeToGoalRequest
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
}
