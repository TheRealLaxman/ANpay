using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.DTOs;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
        if (!result.Success)
            return Unauthorized(result);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!result.Success)
            return Unauthorized(result);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _authService.RevokeRefreshTokenAsync(dto.RefreshToken, userId);
        return Ok(new { message = "Token revoked" });
    }

    [Authorize]
    [HttpPost("transaction-pin/set")]
    public async Task<IActionResult> SetTransactionPin([FromBody] SetPinDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _authService.SetTransactionPinAsync(userId, dto.Pin);
        return Ok(new { message = "Transaction PIN set successfully" });
    }

    [Authorize]
    [HttpPost("transaction-pin/verify")]
    public async Task<IActionResult> VerifyTransactionPin([FromBody] VerifyPinDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var valid = await _authService.VerifyTransactionPinAsync(userId, dto.Pin);
        return Ok(new { valid });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            await _authService.GeneratePasswordResetTokenAsync(dto.Email);
            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }
        catch
        {
            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _authService.ResetPasswordAsync(dto.Email, dto.Token, dto.NewPassword);
        return Ok(new { message = "Password reset successfully" });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
