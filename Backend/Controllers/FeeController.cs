using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;
using System.ComponentModel.DataAnnotations;

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
    public async Task<IActionResult> Create([FromBody] CreateFeeDto dto)
    {
        var fee = new Fee
        {
            Name = dto.Name,
            Type = dto.Type,
            AppliesTo = dto.AppliesTo,
            Value = dto.Value,
            MinAmount = dto.MinAmount,
            MaxAmount = dto.MaxAmount,
            MinFee = dto.MinFee,
            MaxFee = dto.MaxFee,
            Currency = dto.Currency,
            IsActive = dto.IsActive
        };
        var created = await _feeService.CreateAsync(fee);
        return Ok(created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeeDto dto)
    {
        var fee = new Fee
        {
            Name = dto.Name,
            Type = dto.Type,
            AppliesTo = dto.AppliesTo,
            Value = dto.Value,
            MinAmount = dto.MinAmount,
            MaxAmount = dto.MaxAmount,
            MinFee = dto.MinFee,
            MaxFee = dto.MaxFee,
            Currency = dto.Currency,
            IsActive = dto.IsActive
        };
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

public class CreateFeeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public FeeType Type { get; set; }
    public FeeAppliesTo AppliesTo { get; set; }
    public decimal Value { get; set; }
    public decimal MinAmount { get; set; } = 0;
    public decimal MaxAmount { get; set; } = decimal.MaxValue;
    public decimal MinFee { get; set; } = 0;
    public decimal MaxFee { get; set; } = decimal.MaxValue;
    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";
    public bool IsActive { get; set; } = true;
}

public class UpdateFeeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public FeeType Type { get; set; }
    public FeeAppliesTo AppliesTo { get; set; }
    public decimal Value { get; set; }
    public decimal MinAmount { get; set; } = 0;
    public decimal MaxAmount { get; set; } = decimal.MaxValue;
    public decimal MinFee { get; set; } = 0;
    public decimal MaxFee { get; set; } = decimal.MaxValue;
    [MaxLength(10)]
    public string Currency { get; set; } = "NGN";
    public bool IsActive { get; set; } = true;
}
