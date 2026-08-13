using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/market")]
[Authorize]
public class MarketDataController : ControllerBase
{
    private readonly MarketDataService _marketDataService;

    public MarketDataController(MarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    [HttpGet("rates")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllRates()
    {
        var rates = await _marketDataService.GetAllRatesAsync();
        return Ok(rates);
    }

    [HttpGet("rate/{from}/{to}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRate(string from, string to)
    {
        var rate = await _marketDataService.GetRateAsync(from, to);
        if (rate == null)
            return NotFound(new { message = $"Rate {from}/{to} not found" });
        return Ok(rate);
    }

    [HttpGet("history/{from}/{to}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRateHistory(string from, string to)
    {
        var history = await _marketDataService.GetRateHistoryAsync(from, to);
        return Ok(history);
    }
}
