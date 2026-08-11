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

    // Auth
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

    // Profile
    public async Task<UserProfile> GetProfileAsync()
    {
        var result = await _http.GetFromJsonAsync<UserProfile>($"{_baseUrl}/profile");
        return result!;
    }

    public async Task<UserProfile> UpdateProfileAsync(string firstName, string lastName, string phoneNumber)
    {
        var response = await _http.PutAsJsonAsync($"{_baseUrl}/profile", new { firstName, lastName, phoneNumber });
        var result = await response.Content.ReadFromJsonAsync<UserProfile>();
        return result!;
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/profile/change-password", new { currentPassword, newPassword });
    }

    // Wallets
    public async Task<List<WalletDto>> GetWalletsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<WalletDto>>($"{_baseUrl}/wallet");
        return result!;
    }

    public async Task<WalletDto> CreateWalletAsync(string name, string currency)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet", new { walletName = name, currency });
        var result = await response.Content.ReadFromJsonAsync<WalletDto>();
        return result!;
    }

    // Transactions
    public async Task<TransactionDto> DepositAsync(Guid walletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/deposit", new { walletId, amount, description });
        var result = await response.Content.ReadFromJsonAsync<TransactionDto>();
        return result!;
    }

    public async Task<TransactionDto> WithdrawAsync(Guid walletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/withdraw", new { walletId, amount, description });
        var result = await response.Content.ReadFromJsonAsync<TransactionDto>();
        return result!;
    }

    public async Task<TransactionDto> TransferAsync(Guid sourceWalletId, Guid destWalletId, decimal amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/transfer", new { sourceWalletId, destinationWalletId = destWalletId, amount, description });
        var result = await response.Content.ReadFromJsonAsync<TransactionDto>();
        return result!;
    }

    public async Task<TransactionHistory> GetTransactionHistoryAsync(Guid walletId, int page = 1, int pageSize = 20)
    {
        var result = await _http.GetFromJsonAsync<TransactionHistory>($"{_baseUrl}/transaction/wallet/{walletId}?page={page}&pageSize={pageSize}");
        return result!;
    }

    public async Task<TransactionDto?> GetTransactionByIdAsync(Guid transactionId)
    {
        var result = await _http.GetFromJsonAsync<TransactionDto>($"{_baseUrl}/transaction/{transactionId}");
        return result;
    }

    // Beneficiaries
    public async Task<List<BeneficiaryDto>> GetBeneficiariesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<BeneficiaryDto>>($"{_baseUrl}/beneficiary");
        return result ?? new List<BeneficiaryDto>();
    }

    public async Task<BeneficiaryDto> CreateBeneficiaryAsync(string nickname, Guid walletId, string? email)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/beneficiary", new { nickname, walletId, email });
        var result = await response.Content.ReadFromJsonAsync<BeneficiaryDto>();
        return result!;
    }

    public async Task DeleteBeneficiaryAsync(Guid id)
    {
        await _http.DeleteAsync($"{_baseUrl}/beneficiary/{id}");
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
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
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
    public string? DestinationWalletName { get; set; }
}

public class TransactionHistory
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class BeneficiaryDto
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public Guid WalletId { get; set; }
    public string WalletName { get; set; } = string.Empty;
    public string WalletCurrency { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
