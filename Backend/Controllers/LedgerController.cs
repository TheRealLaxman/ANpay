using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LedgerController : ControllerBase
{
    private readonly LedgerService _ledgerService;

    public LedgerController(LedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    [HttpGet("accounts")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _ledgerService.GetBalanceAsync(Guid.Empty);
        return Ok(new { message = "Ledger system active" });
    }
}
