using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BeneficiaryController : ControllerBase
{
    private readonly BeneficiaryService _beneficiaryService;
    private readonly ILogger<BeneficiaryController> _logger;

    public BeneficiaryController(BeneficiaryService beneficiaryService, ILogger<BeneficiaryController> logger)
    {
        _beneficiaryService = beneficiaryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<BeneficiaryDto>>> GetBeneficiaries()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var beneficiaries = await _beneficiaryService.GetUserBeneficiariesAsync(userId);
        return Ok(beneficiaries);
    }

    [HttpPost]
    public async Task<ActionResult<BeneficiaryDto>> CreateBeneficiary([FromBody] CreateBeneficiaryDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var beneficiary = await _beneficiaryService.CreateBeneficiaryAsync(userId, dto);
        return CreatedAtAction(nameof(GetBeneficiaries), beneficiary);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBeneficiary(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _beneficiaryService.DeleteBeneficiaryAsync(id, userId);
        return NoContent();
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
