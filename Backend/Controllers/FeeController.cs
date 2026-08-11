using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeeController : ControllerBase
{
    private readonly FeeService _feeService;

    public FeeController(FeeService feeService)
    {
        _feeService = feeService;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetAll()
    {
        var fees = await _feeService.GetAllAsync();
        return Ok(fees);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] Fee fee)
    {
        var created = await _feeService.CreateAsync(fee);
        return Ok(created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Fee fee)
    {
        var updated = await _feeService.UpdateAsync(id, fee);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _feeService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("calculate")]
    public async Task<IActionResult> Calculate([FromQuery] string appliesTo, [FromQuery] decimal amount, [FromQuery] string currency = "NGN")
    {
        if (!Enum.TryParse<FeeAppliesTo>(appliesTo, true, out var feeType))
            return BadRequest("Invalid fee type");

        var fee = await _feeService.CalculateFeeAsync(feeType, amount, currency);
        return Ok(new { fee, amount, currency });
    }
}
