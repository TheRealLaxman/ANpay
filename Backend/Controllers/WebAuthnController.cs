using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebAuthnController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WebAuthnController> _logger;

    // In-memory store for demo. In production, use a database table.
    private static readonly List<WebAuthnCredentialStore> _credentials = new();
    private static readonly List<WebAuthnChallengeStore> _challenges = new();

    public WebAuthnController(UserManager<ApplicationUser> userManager, ILogger<WebAuthnController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("challenge")]
    public IActionResult GetChallenge()
    {
        var challenge = GenerateChallenge();
        return Ok(new { challenge, rpId = HttpContext.Request.Host.Host });
    }

    [HttpPost("login-challenge")]
    public IActionResult GetLoginChallenge([FromBody] LoginChallengeRequest request)
    {
        var challenge = GenerateChallenge();
        var userCreds = _credentials.Where(c => c.UserEmail == request.Email).ToList();

        return Ok(new
        {
            challenge,
            rpId = HttpContext.Request.Host.Host,
            credentials = userCreds.Select(c => new { id = c.CredentialId, type = "public-key" })
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCredentialRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        _credentials.Add(new WebAuthnCredentialStore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = user.Email ?? "",
            CredentialId = request.CredentialId,
            PublicKey = request.PublicKey,
            DeviceName = request.DeviceName,
            Counter = request.Counter,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation("WebAuthn credential registered for {Email}", user.Email);
        return Ok(new { message = "Credential registered" });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyCredentialRequest request)
    {
        var credential = _credentials.FirstOrDefault(c => c.CredentialId == request.CredentialId);
        if (credential == null)
            return Ok(new { success = false, error = "Credential not found" });

        credential.Counter++;
        var user = await _userManager.FindByIdAsync(credential.UserId);
        if (user == null)
            return Ok(new { success = false, error = "User not found" });

        // In production, verify the signature against the public key
        // For demo, we just check the credential exists

        var token = GenerateJwtToken(user);

        return Ok(new
        {
            success = true,
            token,
            userId = user.Id,
            email = user.Email,
            role = user.Role.ToString(),
            firstName = user.FirstName,
            lastName = user.LastName
        });
    }

    [Authorize]
    [HttpGet("my")]
    public IActionResult GetMyCredentials()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var creds = _credentials.Where(c => c.UserId == userId).Select(c => new
        {
            id = c.Id,
            credentialId = c.CredentialId,
            deviceName = c.DeviceName,
            createdAt = c.CreatedAt
        }).ToList();

        return Ok(creds);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public IActionResult RemoveCredential(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var cred = _credentials.FirstOrDefault(c => c.Id == id && c.UserId == userId);
        if (cred != null)
            _credentials.Remove(cred);

        return Ok(new { message = "Credential removed" });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string GenerateChallenge()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        // Simplified token generation for WebAuthn login
        var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("ANpay-SuperSecret-Key-2026-Must-Change-This-In-Production!"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            securityKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "ANpay",
            audience: "ANpay",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class WebAuthnCredentialStore
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int Counter { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WebAuthnChallengeStore
{
    public string Challenge { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class LoginChallengeRequest
{
    public string Email { get; set; } = string.Empty;
}

public class RegisterCredentialRequest
{
    public string CredentialId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int Counter { get; set; }
}

public class VerifyCredentialRequest
{
    public string CredentialId { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string ClientDataJSON { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
