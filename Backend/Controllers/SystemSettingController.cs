using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/system")]
[Authorize(Roles = "SuperAdmin")]
public class SystemSettingController : ControllerBase
{
    private readonly SystemSettingService _settingService;
    private readonly ILogger<SystemSettingController> _logger;

    public SystemSettingController(SystemSettingService settingService, ILogger<SystemSettingController> logger)
    {
        _settingService = settingService;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _settingService.GetAllAsync();
        return Ok(settings);
    }

    [HttpGet("settings/{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        var settings = await _settingService.GetByCategoryAsync(category);
        return Ok(settings);
    }

    [HttpGet("settings/key/{key}")]
    public async Task<IActionResult> GetByKey(string key)
    {
        var setting = await _settingService.GetByKeyAsync(key);
        if (setting == null) return NotFound();
        return Ok(setting);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Set([FromBody] SetSettingDto dto)
    {
        var setting = await _settingService.SetAsync(dto.Key, dto.Value, dto.Category, dto.Description);
        return Ok(setting);
    }

    [HttpDelete("settings/{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        await _settingService.DeleteAsync(key);
        return Ok(new { message = "Setting deleted" });
    }
}

public class SetSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
}
