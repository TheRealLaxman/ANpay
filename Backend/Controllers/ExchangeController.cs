using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExchangeController : ControllerBase
{
    private readonly ExchangeService _exchangeService;

    public ExchangeController(ExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
    }

    [HttpGet("rates")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRates()
    {
        var rates = await _exchangeService.GetAllAsync();
        return Ok(rates);
    }

    [HttpGet("rate")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRate([FromQuery] string from, [FromQuery] string to)
    {
        var rate = await _exchangeService.GetRateAsync(from, to);
        return Ok(rate);
    }

    [HttpPost("rates")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpsertRate([FromBody] UpsertRateDto dto)
    {
        var rate = await _exchangeService.UpsertRateAsync(dto.FromCurrency, dto.ToCurrency, dto.BuyRate, dto.SellRate);
        return Ok(rate);
    }

    [HttpGet("quote")]
    public async Task<IActionResult> GetQuote([FromQuery] string from, [FromQuery] string to, [FromQuery] decimal amount)
    {
        var quote = await _exchangeService.GetQuoteAsync(from, to, amount);
        return Ok(quote);
    }
}

public class UpsertRateDto
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
}
