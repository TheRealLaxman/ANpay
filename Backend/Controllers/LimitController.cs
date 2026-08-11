using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LimitController : ControllerBase
{
    private readonly LimitService _limitService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public LimitController(LimitService limitService)
    {
        _limitService = limitService;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetAll()
    {
        var limits = await _limitService.GetAllAsync();
        return Ok(limits);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] TransactionLimit limit)
    {
        var created = await _limitService.CreateAsync(limit);
        return Ok(created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLimitDto dto)
    {
        var updated = await _limitService.UpdateAsync(id, dto.LimitAmount);
        return Ok(updated);
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] string type, [FromQuery] decimal amount, [FromQuery] string currency = "NGN")
    {
        if (!Enum.TryParse<TransactionLimitType>(type, true, out var limitType))
            return BadRequest("Invalid limit type");

        var withinLimit = await _limitService.CheckLimitAsync(UserId, limitType, amount, currency);
        return Ok(new { withinLimit, amount, type, currency });
    }
}

public class UpdateLimitDto
{
    public decimal LimitAmount { get; set; }
}
