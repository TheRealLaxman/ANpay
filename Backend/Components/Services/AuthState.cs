using Microsoft.JSInterop;

namespace ANpay.Api.Components.Services;

public class AuthState
{
    private readonly IJSRuntime _js;
    private readonly ApiService _api;

    public bool IsLoggedIn { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsPinSet { get; set; }

    public event Action? OnChange;

    public AuthState(IJSRuntime js, ApiService api)
    {
        _js = js;
        _api = api;
    }

    public async Task InitializeAsync()
    {
        try
        {
            Token = await _js.InvokeAsync<string>("localStorage.getItem", "token") ?? string.Empty;
            Role = await _js.InvokeAsync<string>("localStorage.getItem", "role") ?? string.Empty;
            Email = await _js.InvokeAsync<string>("localStorage.getItem", "email") ?? string.Empty;
            FirstName = await _js.InvokeAsync<string>("localStorage.getItem", "firstName") ?? string.Empty;
            LastName = await _js.InvokeAsync<string>("localStorage.getItem", "lastName") ?? string.Empty;
            UserId = await _js.InvokeAsync<string>("localStorage.getItem", "userId") ?? string.Empty;

            if (!string.IsNullOrEmpty(Token))
            {
                IsLoggedIn = true;
                _api.SetToken(Token);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public async Task LoginAsync(string token, string userId, string email, string role, string firstName, string lastName)
    {
        Token = token;
        UserId = userId;
        Email = email;
        Role = role;
        FirstName = firstName;
        LastName = lastName;
        IsLoggedIn = true;

        await _js.InvokeVoidAsync("localStorage.setItem", "token", token);
        await _js.InvokeVoidAsync("localStorage.setItem", "userId", userId);
        await _js.InvokeVoidAsync("localStorage.setItem", "email", email);
        await _js.InvokeVoidAsync("localStorage.setItem", "role", role);
        await _js.InvokeVoidAsync("localStorage.setItem", "firstName", firstName);
        await _js.InvokeVoidAsync("localStorage.setItem", "lastName", lastName);

        _api.SetToken(Token);
        OnChange?.Invoke();
    }

    public async Task LogoutAsync()
    {
        Token = string.Empty;
        UserId = string.Empty;
        Email = string.Empty;
        Role = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        IsLoggedIn = false;

        await _js.InvokeVoidAsync("localStorage.removeItem", "token");
        await _js.InvokeVoidAsync("localStorage.removeItem", "userId");
        await _js.InvokeVoidAsync("localStorage.removeItem", "email");
        await _js.InvokeVoidAsync("localStorage.removeItem", "role");
        await _js.InvokeVoidAsync("localStorage.removeItem", "firstName");
        await _js.InvokeVoidAsync("localStorage.removeItem", "lastName");

        OnChange?.Invoke();
    }

    public string GetDisplayName()
    {
        return $"{FirstName} {LastName}".Trim();
    }

    public bool IsSuperAdmin() => Role == "SuperAdmin";
    public bool IsBranchAdmin() => Role == "BranchAdmin";
    public bool IsOfficial() => Role == "Official";
    public bool IsCustomer() => Role == "Customer";
}
