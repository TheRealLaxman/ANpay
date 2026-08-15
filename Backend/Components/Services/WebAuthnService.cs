using Microsoft.JSInterop;

namespace ANpay.Api.Components.Services;

public class WebAuthnService
{
    private readonly IJSRuntime _js;
    private readonly ApiService _api;
    private readonly ILogger<WebAuthnService> _logger;

    public WebAuthnService(IJSRuntime js, ApiService api, ILogger<WebAuthnService> logger)
    {
        _js = js;
        _api = api;
        _logger = logger;
    }

    public async Task<bool> IsWebAuthnAvailableAsync()
    {
        try { return await _js.InvokeAsync<bool>("webauthn.isAvailable"); }
        catch { return false; }
    }

    public async Task<WebAuthnRegistrationResult> RegisterAsync(string userId, string email, string displayName)
    {
        try
        {
            var challenge = await _api.GetWebAuthnChallengeAsync();
            var result = await _js.InvokeAsync<System.Text.Json.JsonElement>("webauthn.register", new
            {
                challenge = challenge.Challenge,
                rp = new { name = "ANpay", id = challenge.RpId },
                user = new { id = userId, name = email, displayName },
                pubKeyCredParams = new[]
                {
                    new { alg = -7, type = "public-key" },
                    new { alg = -257, type = "public-key" }
                },
                authenticatorSelection = new { authenticatorAttachment = "platform", userVerification = "preferred" },
                timeout = 60000,
                attestation = "none"
            });

            await _api.RegisterWebAuthnAsync(new WebAuthnCredentialRequest
            {
                CredentialId = result.GetProperty("id").GetString() ?? "",
                PublicKey = result.GetProperty("rawId").GetString() ?? "",
                DeviceName = await GetDeviceNameAsync()
            });

            return new WebAuthnRegistrationResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebAuthn registration failed");
            return new WebAuthnRegistrationResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<WebAuthnLoginResult> AuthenticateAsync(string email)
    {
        try
        {
            var challenge = await _api.GetWebAuthnLoginChallengeAsync(email);
            var result = await _js.InvokeAsync<System.Text.Json.JsonElement>("webauthn.authenticate", new
            {
                challenge = challenge.Challenge,
                rpId = challenge.RpId,
                allowCredentials = challenge.Credentials.Select(c => new { id = c.CredentialId, type = "public-key" }).ToArray(),
                userVerification = "preferred",
                timeout = 60000
            });

            var loginResult = await _api.VerifyWebAuthnAsync(new WebAuthnVerifyRequest
            {
                CredentialId = result.GetProperty("id").GetString() ?? "",
                AuthenticatorData = result.GetProperty("response").GetProperty("authenticatorData").GetString() ?? "",
                ClientDataJSON = result.GetProperty("response").GetProperty("clientDataJSON").GetString() ?? "",
                Signature = result.GetProperty("response").GetProperty("signature").GetString() ?? ""
            });

            return new WebAuthnLoginResult
            {
                Success = loginResult.Success,
                Token = loginResult.Token,
                UserId = loginResult.UserId,
                Email = loginResult.Email,
                Role = loginResult.Role,
                FirstName = loginResult.FirstName,
                LastName = loginResult.LastName,
                Error = loginResult.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebAuthn authentication failed");
            return new WebAuthnLoginResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<WebAuthnCredentialInfo>> GetMyCredentialsAsync()
    {
        try { return await _api.GetMyWebAuthnCredentialsAsync(); }
        catch { return new List<WebAuthnCredentialInfo>(); }
    }

    public async Task RemoveCredentialAsync(Guid id)
    {
        await _api.RemoveWebAuthnCredentialAsync(id);
    }

    private async Task<string> GetDeviceNameAsync()
    {
        try { return await _js.InvokeAsync<string>("webauthn.getDeviceName"); }
        catch { return "Unknown Device"; }
    }
}

public class WebAuthnRegistrationResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
}

public class WebAuthnLoginResult
{
    public bool Success { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
