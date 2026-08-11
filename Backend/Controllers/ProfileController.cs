using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(AuthService authService, ILogger<ProfileController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var profile = await _authService.GetProfileAsync(userId);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var profile = await _authService.UpdateProfileAsync(userId, dto);
        return Ok(profile);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _authService.ChangePasswordAsync(userId, dto);
        return Ok(new { message = "Password changed successfully" });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
