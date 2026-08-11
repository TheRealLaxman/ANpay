using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(TransactionService transactionService, ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    [HttpGet("wallet/{walletId}")]
    public async Task<ActionResult<TransactionHistoryDto>> GetTransactionHistory(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var history = await _transactionService.GetTransactionHistoryAsync(
            walletId, userId, page, pageSize);
        return Ok(history);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetTransaction(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var transaction = await _transactionService.GetTransactionByIdAsync(id, userId);
        if (transaction == null)
            return NotFound();
        return Ok(transaction);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
