using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenBankingController : ControllerBase
{
    private readonly OpenBankingService _openBankingService;
    private readonly ILogger<OpenBankingController> _logger;

    public OpenBankingController(OpenBankingService openBankingService, ILogger<OpenBankingController> logger)
    {
        _openBankingService = openBankingService;
        _logger = logger;
    }

    [HttpPost("api-keys")]
    public async Task<ActionResult<ApiKey>> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var apiKey = await _openBankingService.CreateApiKeyAsync(userId, request.Name, request.Scope, request.MaxUsagePerDay, request.ExpiryDays);
        return Ok(apiKey);
    }

    [HttpGet("api-keys")]
    public async Task<ActionResult<List<ApiKey>>> GetMyApiKeys()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var keys = await _openBankingService.GetUserApiKeysAsync(userId);
        return Ok(keys);
    }

    [HttpPost("api-keys/{id}/revoke")]
    public async Task<ActionResult<ApiKey>> RevokeApiKey(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var key = await _openBankingService.RevokeApiKeyAsync(id, userId);
        return Ok(key);
    }

    [HttpPost("webhooks")]
    public async Task<ActionResult<Webhook>> CreateWebhook([FromBody] CreateWebhookRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var webhook = await _openBankingService.CreateWebhookAsync(userId, request.Url, request.Name, request.Events);
        return Ok(webhook);
    }

    [HttpGet("webhooks")]
    public async Task<ActionResult<List<Webhook>>> GetMyWebhooks()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var webhooks = await _openBankingService.GetUserWebhooksAsync(userId);
        return Ok(webhooks);
    }

    [HttpPost("webhooks/{id}/toggle")]
    public async Task<ActionResult<Webhook>> ToggleWebhook(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var webhook = await _openBankingService.ToggleWebhookAsync(id, userId);
        return Ok(webhook);
    }

    [HttpPost("bank-accounts")]
    public async Task<ActionResult<OpenBankingAccount>> ConnectBankAccount([FromBody] ConnectBankRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var account = await _openBankingService.ConnectBankAccountAsync(
            userId, request.BankName, request.BankCode, request.AccountNumber, request.AccountName, request.AccountType, request.Currency);

        return Ok(account);
    }

    [HttpGet("bank-accounts")]
    public async Task<ActionResult<List<OpenBankingAccount>>> GetMyBankAccounts()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var accounts = await _openBankingService.GetUserBankAccountsAsync(userId);
        return Ok(accounts);
    }

    [HttpPost("bank-accounts/{id}/disconnect")]
    public async Task<ActionResult<OpenBankingAccount>> DisconnectBankAccount(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var account = await _openBankingService.DisconnectBankAccountAsync(id, userId);
        return Ok(account);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.Read;
    public int MaxUsagePerDay { get; set; } = 1000;
    public int ExpiryDays { get; set; } = 365;
}

public class CreateWebhookRequest
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Events { get; set; }
}

public class ConnectBankRequest
{
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Savings;
    public string Currency { get; set; } = "NGN";
}
