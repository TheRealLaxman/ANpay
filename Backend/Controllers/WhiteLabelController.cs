using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class WhiteLabelController : ControllerBase
{
    private readonly WhiteLabelService _whiteLabelService;
    private readonly ILogger<WhiteLabelController> _logger;

    public WhiteLabelController(WhiteLabelService whiteLabelService, ILogger<WhiteLabelController> logger)
    {
        _whiteLabelService = whiteLabelService;
        _logger = logger;
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<WhiteLabelTenant>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var tenant = await _whiteLabelService.CreateTenantAsync(
            request.CompanyName, request.ContactEmail, request.ContactPhone, request.Plan, request.MaxUsers);

        return Ok(tenant);
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<List<WhiteLabelTenant>>> GetAllTenants()
    {
        var tenants = await _whiteLabelService.GetAllTenantsAsync();
        return Ok(tenants);
    }

    [HttpGet("tenants/{id}")]
    public async Task<ActionResult<WhiteLabelTenant>> GetTenant(Guid id)
    {
        var tenant = await _whiteLabelService.GetTenantByIdAsync(id);
        if (tenant == null) return NotFound();
        return Ok(tenant);
    }

    [HttpPost("tenants/{id}/activate")]
    public async Task<ActionResult<WhiteLabelTenant>> ActivateTenant(Guid id)
    {
        var tenant = await _whiteLabelService.ActivateTenantAsync(id);
        return Ok(tenant);
    }

    [HttpPost("tenants/{id}/suspend")]
    public async Task<ActionResult<WhiteLabelTenant>> SuspendTenant(Guid id)
    {
        var tenant = await _whiteLabelService.SuspendTenantAsync(id);
        return Ok(tenant);
    }

    [HttpPost("tenants/{id}/users")]
    public async Task<ActionResult<TenantUser>> AddUserToTenant(Guid id, [FromBody] AddTenantUserRequest request)
    {
        var tenantUser = await _whiteLabelService.AddUserToTenantAsync(id, request.UserId, request.Role);
        return Ok(tenantUser);
    }

    [HttpGet("tenants/{id}/users")]
    public async Task<ActionResult<List<TenantUser>>> GetTenantUsers(Guid id)
    {
        var users = await _whiteLabelService.GetTenantUsersAsync(id);
        return Ok(users);
    }

    [HttpPut("tenants/{id}/settings")]
    public async Task<ActionResult<WhiteLabelTenant>> UpdateTenantSettings(Guid id, [FromBody] UpdateTenantSettingsRequest request)
    {
        var tenant = await _whiteLabelService.UpdateTenantSettingsAsync(
            id, request.LogoUrl, request.PrimaryColor, request.SecondaryColor, request.CustomDomain, request.TransactionFeePercentage);

        return Ok(tenant);
    }
}

public class CreateTenantRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public WhiteLabelPlan Plan { get; set; } = WhiteLabelPlan.Basic;
    public int MaxUsers { get; set; } = 1000;
}

public class AddTenantUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public TenantUserRole Role { get; set; } = TenantUserRole.User;
}

public class UpdateTenantSettingsRequest
{
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? CustomDomain { get; set; }
    public decimal? TransactionFeePercentage { get; set; }
}
