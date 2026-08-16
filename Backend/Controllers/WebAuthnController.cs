using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebAuthnController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebAuthnController> _logger;
    private readonly IConfiguration _configuration;

    public WebAuthnController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<WebAuthnController> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("challenge")]
    public async Task<IActionResult> GetChallenge()
    {
        var challenge = GenerateChallenge();
        var challengeRecord = new WebAuthnChallenge
        {
            Challenge = challenge,
            UserId = GetUserId() ?? "anonymous",
            Purpose = "register",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.WebAuthnChallenges.Add(challengeRecord);
        await _context.SaveChangesAsync();

        return Ok(new { challenge, rpId = HttpContext.Request.Host.Host });
    }

    [HttpPost("login-challenge")]
    public async Task<IActionResult> GetLoginChallenge([FromBody] LoginChallengeRequest request)
    {
        var challenge = GenerateChallenge();
        var user = await _userManager.FindByEmailAsync(request.Email);

        var challengeRecord = new WebAuthnChallenge
        {
            Challenge = challenge,
            UserId = user?.Id ?? "anonymous",
            Purpose = "login",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.WebAuthnChallenges.Add(challengeRecord);
        await _context.SaveChangesAsync();

        var userCreds = user != null
            ? await _context.WebAuthnCredentials
                .Where(c => c.UserId == user.Id && c.IsActive)
                .ToListAsync()
            : new List<WebAuthnCredential>();

        return Ok(new
        {
            challenge,
            rpId = HttpContext.Request.Host.Host,
            credentials = userCreds.Select(c => new { id = c.CredentialId, type = "public-key" })
        });
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCredentialRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        // Check if credential already exists
        var existing = await _context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == request.CredentialId);
        if (existing != null)
            return BadRequest(new { message = "Credential already registered" });

        var credential = new WebAuthnCredential
        {
            UserId = userId,
            CredentialId = request.CredentialId,
            PublicKey = request.PublicKey,
            DeviceName = request.DeviceName,
            Counter = request.Counter,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        _context.WebAuthnCredentials.Add(credential);
        await _context.SaveChangesAsync();

        _logger.LogInformation("WebAuthn credential registered for {Email}", user.Email);
        return Ok(new { message = "Credential registered", credentialId = credential.Id });
    }

    [AllowAnonymous]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyCredentialRequest request)
    {
        if (string.IsNullOrEmpty(request.CredentialId))
            return BadRequest(new { success = false, error = "CredentialId is required" });

        // ChallengeId is required for login — prevents replay attacks
        if (string.IsNullOrEmpty(request.ChallengeId))
            return BadRequest(new { success = false, error = "ChallengeId is required for login" });

        var credential = await _context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == request.CredentialId && c.IsActive);
        if (credential == null)
            return Ok(new { success = false, error = "Credential not found" });

        if (string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.AuthenticatorData) || string.IsNullOrEmpty(request.ClientDataJSON))
            return BadRequest(new { success = false, error = "Missing authenticator response fields" });

        // Validate challenge — must be provided and valid
        var challenge = await _context.WebAuthnChallenges
            .FirstOrDefaultAsync(c => c.Id.ToString() == request.ChallengeId && !c.IsUsed && c.ExpiresAt > DateTime.UtcNow);
        if (challenge == null)
            return BadRequest(new { success = false, error = "Invalid or expired challenge" });

        // Verify the challenge matches the expected value from clientDataJSON
        if (!string.IsNullOrEmpty(challenge.Challenge))
        {
            try
            {
                var clientData = JsonSerializer.Deserialize<JsonElement>(request.ClientDataJSON);
                var challengeFromClient = clientData.TryGetProperty("challenge", out var ch) ? ch.GetString() : null;
                if (challengeFromClient != challenge.Challenge)
                    return BadRequest(new { success = false, error = "Challenge mismatch" });
            }
            catch
            {
                return BadRequest(new { success = false, error = "Invalid clientDataJSON" });
            }
        }

        challenge.IsUsed = true;

        // Verify the signature using the stored public key
        // Per FIDO2 spec: signed data = authenticatorData || SHA256(clientDataJSON)
        try
        {
            var authenticatorDataBytes = Convert.FromBase64String(request.AuthenticatorData);
            var clientDataJsonBytes = Encoding.UTF8.GetBytes(request.ClientDataJSON);

            using var sha256 = SHA256.Create();
            var clientDataHash = sha256.ComputeHash(clientDataJsonBytes);

            // Concatenate authenticatorData + SHA256(clientDataJSON)
            var dataToVerify = new byte[authenticatorDataBytes.Length + 32];
            Array.Copy(authenticatorDataBytes, 0, dataToVerify, 0, authenticatorDataBytes.Length);
            Array.Copy(clientDataHash, 0, dataToVerify, authenticatorDataBytes.Length, 32);

            var signatureBytes = Convert.FromBase64String(request.Signature);
            var publicKeyBytes = Convert.FromBase64String(credential.PublicKey);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            // Hash the concatenated data
            var hash = sha256.ComputeHash(dataToVerify);

            var isValid = ecdsa.VerifyHash(hash, signatureBytes);
            if (!isValid)
                return Ok(new { success = false, error = "Signature verification failed" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn signature verification error for credential {CredentialId}", request.CredentialId);
            return Ok(new { success = false, error = "Signature verification error" });
        }

        credential.LastUsedAt = DateTime.UtcNow;

        // Validate FIDO2 counter — must be > stored counter to detect cloned authenticators
        if (request.Counter <= credential.Counter)
            return Ok(new { success = false, error = "Authenticator counter validation failed" });

        credential.Counter = request.Counter;
        await _context.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(credential.UserId);
        if (user == null)
            return Ok(new { success = false, error = "User not found" });

        var jwtToken = GenerateJwtToken(user);

        return Ok(new
        {
            success = true,
            token = jwtToken,
            userId = user.Id,
            email = user.Email,
            role = user.Role.ToString(),
            firstName = user.FirstName,
            lastName = user.LastName
        });
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyCredentials()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var creds = await _context.WebAuthnCredentials
            .Where(c => c.UserId == userId && c.IsActive)
            .Select(c => new
            {
                id = c.Id,
                credentialId = c.CredentialId,
                deviceName = c.DeviceName,
                createdAt = c.CreatedAt,
                lastUsedAt = c.LastUsedAt,
                counter = c.Counter
            })
            .OrderByDescending(c => c.lastUsedAt)
            .ToListAsync();

        return Ok(creds);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveCredential(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var cred = await _context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (cred != null)
        {
            cred.IsActive = false;
            await _context.SaveChangesAsync();
        }

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
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpirationInMinutes"] ?? "15"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
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
    public string? ChallengeId { get; set; }
    public int Counter { get; set; }
}
