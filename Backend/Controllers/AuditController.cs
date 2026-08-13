using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditController(AuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> Get([FromQuery] string? userId, [FromQuery] string? action,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        var logs = await _auditService.GetAsync(userId, action, from, to, skip, take);
        return Ok(logs);
    }

    [HttpGet("verify-integrity")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> VerifyIntegrity()
    {
        var isValid = await _auditService.VerifyIntegrityAsync();
        return Ok(new { success = true, data = new { isValid, message = isValid ? "Audit log integrity verified" : "Integrity breach detected" } });
    }
}
