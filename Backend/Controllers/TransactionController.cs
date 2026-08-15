using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using ANpay.Api.DTOs;
using ANpay.Api.Services;
using ANpay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(TransactionService transactionService, ApplicationDbContext context, ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _context = context;
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

    [HttpGet("wallet/{walletId}/export")]
    public async Task<IActionResult> ExportTransactions(
        Guid walletId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string format = "csv")
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) return NotFound("Wallet not found");

        var query = _context.Transactions
            .Where(t => t.WalletId == walletId)
            .AsQueryable();

        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);

        var transactions = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Type,Description,Amount,Fee,Status,Reference");

        foreach (var t in transactions)
        {
            sb.AppendLine($"{t.CreatedAt:yyyy-MM-dd HH:mm:ss},{t.Type},{EscapeCsv(t.Description)},{t.Amount},{t.Fee},{t.Status},{t.ReferenceNumber}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"transactions_{walletId}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("wallet/{walletId}/analytics")]
    public async Task<ActionResult<SpendingAnalyticsDto>> GetSpendingAnalytics(
        Guid walletId,
        [FromQuery] int months = 6)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId);
        if (wallet == null) return NotFound("Wallet not found");

        var since = DateTime.UtcNow.AddMonths(-months);
        var transactions = await _context.Transactions
            .Where(t => t.WalletId == walletId && t.CreatedAt >= since && t.Status == Models.TransactionStatus.Completed)
            .ToListAsync();

        var totalSpent = transactions.Where(t => t.Type == Models.TransactionType.Withdrawal || t.Type == Models.TransactionType.TransferOut || t.Type == Models.TransactionType.Payment).Sum(t => t.Amount);
        var totalReceived = transactions.Where(t => t.Type == Models.TransactionType.Deposit || t.Type == Models.TransactionType.TransferIn).Sum(t => t.Amount);
        var count = transactions.Count;

        var byCategory = transactions
            .GroupBy(t => t.Type.ToString())
            .Select(g => new CategoryBreakdown
            {
                Category = g.Key,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count(),
                Percentage = count > 0 ? Math.Round(g.Count() * 100m / count, 1) : 0
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        var monthly = transactions
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new MonthlySpending
            {
                Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                Amount = g.Sum(t => t.Amount)
            })
            .OrderBy(m => m.Month)
            .ToList();

        return Ok(new SpendingAnalyticsDto
        {
            TotalSpent = totalSpent,
            TotalReceived = totalReceived,
            AverageTransaction = count > 0 ? Math.Round((totalSpent + totalReceived) / count, 2) : 0,
            TransactionCount = count,
            ByCategory = byCategory,
            MonthlyTrend = monthly
        });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
