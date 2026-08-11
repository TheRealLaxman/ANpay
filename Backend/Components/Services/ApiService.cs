using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ANpay.Api.Components.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl = "/api";

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/auth/login", new { email, password });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!;
    }

    public async Task<UserProfile> GetProfileAsync()
    {
        return await _http.GetFromJsonAsync<UserProfile>($"{_baseUrl}/auth/profile")!;
    }

    public async Task<List<WalletDto>> GetWalletsAsync()
    {
        return await _http.GetFromJsonAsync<List<WalletDto>>($"{_baseUrl}/wallet")!;
    }

    public async Task<WalletDto> CreateWalletAsync(string name, string currency)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet", new { walletName = name, currency });
        return await response.Content.ReadFromJsonAsync<WalletDto>()!;
    }

    public async Task<TransactionDto> DepositAsync(Guid walletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/deposit", new { walletId, amount, description });
        return await response.Content.ReadFromJsonAsync<TransactionDto>()!;
    }

    public async Task<TransactionDto> WithdrawAsync(Guid walletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/withdraw", new { walletId, amount, description });
        return await response.Content.ReadFromJsonAsync<TransactionDto>()!;
    }

    public async Task<TransactionDto> TransferAsync(Guid sourceWalletId, Guid destWalletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/transfer", new { sourceWalletId, destinationWalletId = destWalletId, amount, description });
        return await response.Content.ReadFromJsonAsync<TransactionDto>()!;
    }

    public async Task<TransactionHistory> GetTransactionHistoryAsync(Guid walletId, int page = 1, int pageSize = 20)
    {
        return await _http.GetFromJsonAsync<TransactionHistory>($"{_baseUrl}/transaction/wallet/{walletId}?page={page}&pageSize={pageSize}")!;
    }
}

// DTOs
public class AuthResponse
{
    public bool Success { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WalletDto
{
    public Guid Id { get; set; }
    public string WalletName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TransactionHistory
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
