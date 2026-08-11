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

    // Branches
    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        return await _http.GetFromJsonAsync<List<BranchDto>>($"{_baseUrl}/branch") ?? new();
    }

    public async Task<BranchDto> CreateBranchAsync(string name, string address, string city, string phone)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/branch", new { name, address, city, phone });
        return (await response.Content.ReadFromJsonAsync<BranchDto>())!;
    }

    public async Task<BranchDashboardDto> GetBranchDashboardAsync(Guid branchId)
    {
        return (await _http.GetFromJsonAsync<BranchDashboardDto>($"{_baseUrl}/branch/{branchId}/dashboard"))!;
    }

    // Employees
    public async Task<List<EmployeeDto>> GetEmployeesAsync(Guid branchId)
    {
        return await _http.GetFromJsonAsync<List<EmployeeDto>>($"{_baseUrl}/employee/branch/{branchId}") ?? new();
    }

    // Reports
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        return (await _http.GetFromJsonAsync<DashboardStatsDto>($"{_baseUrl}/report/dashboard"))!;
    }

    // Approvals
    public async Task<List<ApprovalDto>> GetPendingApprovalsAsync()
    {
        return await _http.GetFromJsonAsync<List<ApprovalDto>>($"{_baseUrl}/approval/pending") ?? new();
    }

    public async Task ApproveRequestAsync(Guid id, string notes = "")
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/approval/{id}/approve", new { notes });
    }

    public async Task RejectRequestAsync(Guid id, string notes)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/approval/{id}/reject", new { notes });
    }

    // KYC
    public async Task<List<KycDto>> GetPendingKycAsync()
    {
        return await _http.GetFromJsonAsync<List<KycDto>>($"{_baseUrl}/kyc/pending") ?? new();
    }

    public async Task ReviewKycAsync(Guid profileId, bool approve, string notes)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/kyc/{profileId}/review", new { approve, notes });
    }

    // Notifications
    public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false)
    {
        return await _http.GetFromJsonAsync<List<NotificationDto>>($"{_baseUrl}/notification?unreadOnly={unreadOnly}") ?? new();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await _http.GetFromJsonAsync<UnreadCountResult>($"{_baseUrl}/notification/unread-count");
        return result?.Count ?? 0;
    }

    public async Task MarkNotificationReadAsync(Guid id)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/notification/{id}/read", new { });
    }

    // Support Tickets
    public async Task<List<TicketDto>> GetOpenTicketsAsync()
    {
        return await _http.GetFromJsonAsync<List<TicketDto>>($"{_baseUrl}/support/tickets/open") ?? new();
    }

    // Exchange Rates
    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync()
    {
        return await _http.GetFromJsonAsync<List<ExchangeRateDto>>($"{_baseUrl}/exchange/rates") ?? new();
    }

    // Audit Logs
    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int skip = 0, int take = 100)
    {
        return await _http.GetFromJsonAsync<List<AuditLogDto>>($"{_baseUrl}/audit?skip={skip}&take={take}") ?? new();
    }
}

// New DTOs
public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BranchDashboardDto
{
    public string BranchName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal TodayDeposits { get; set; }
    public decimal TodayWithdrawals { get; set; }
    public int TodayTransfers { get; set; }
    public int PendingApprovals { get; set; }
    public decimal CashBalance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class EmployeeDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SubRole { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalWallets { get; set; }
    public decimal TotalBalance { get; set; }
    public int TotalBranches { get; set; }
    public int TodayTransactions { get; set; }
    public int MonthTransactions { get; set; }
    public decimal TodayVolume { get; set; }
    public decimal MonthVolume { get; set; }
    public int PendingApprovals { get; set; }
    public int PendingKyc { get; set; }
    public int OpenTickets { get; set; }
}

public class ApprovalDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class KycDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountResult
{
    public int Count { get; set; }
}

public class TicketDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExchangeRateDto
{
    public Guid Id { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string OldValues { get; set; } = string.Empty;
    public string NewValues { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public DateTime CreatedAt { get; set; }
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
