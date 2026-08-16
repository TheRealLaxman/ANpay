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

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            return await _http.GetFromJsonAsync<T>($"{_baseUrl}/{endpoint.TrimStart('/')}");
        }
        catch { return default; }
    }

    public async Task<T?> PostAsync<T>(string endpoint, object? data)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/{endpoint.TrimStart('/')}", data);
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch { return default; }
    }

    // Auth
    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/auth/login", new { email, password });
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result == null)
            throw new Exception("Invalid response from server");
        return result;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result == null)
            throw new Exception("Invalid response from server");
        return result;
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

    public async Task<BranchDashboardDto> GetMyBranchDashboardAsync()
    {
        return (await _http.GetFromJsonAsync<BranchDashboardDto>($"{_baseUrl}/branch/my/dashboard"))!;
    }

    // Employees
    public async Task<List<EmployeeDto>> GetEmployeesAsync(Guid branchId)
    {
        return await _http.GetFromJsonAsync<List<EmployeeDto>>($"{_baseUrl}/employee/branch/{branchId}") ?? new();
    }

    public async Task CreateEmployeeAsync(Guid branchId, string email, string subRole)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/employee", new { branchId, email, subRole });
    }

    public async Task UpdateEmployeeSubRoleAsync(Guid id, string subRole)
    {
        await _http.PutAsJsonAsync($"{_baseUrl}/employee/{id}/subrole", new { subRole });
    }

    public async Task DeleteEmployeeAsync(Guid id)
    {
        await _http.DeleteAsync($"{_baseUrl}/employee/{id}");
    }

    // Fees
    public async Task<List<FeeDto>> GetFeesAsync()
    {
        return await _http.GetFromJsonAsync<List<FeeDto>>($"{_baseUrl}/fee") ?? new();
    }

    public async Task CreateFeeAsync(string name, string type, string appliesTo, decimal value, decimal minFee, decimal maxFee)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/fee", new { name, type, appliesTo, value, minFee, maxFee });
    }

    // Limits
    public async Task<List<LimitDto>> GetLimitsAsync()
    {
        return await _http.GetFromJsonAsync<List<LimitDto>>($"{_baseUrl}/limit") ?? new();
    }

    public async Task CreateLimitAsync(string roleName, string limitType, decimal limitAmount, string currency)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/limit", new { roleName, limitType, limitAmount, currency });
    }

    // Exchange Rates
    public async Task UpsertExchangeRateAsync(string fromCurrency, string toCurrency, decimal buyRate, decimal sellRate)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/exchange/rates", new { fromCurrency, toCurrency, buyRate, sellRate });
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

    public async Task<KycProfileDto?> GetMyKycAsync()
    {
        return await _http.GetFromJsonAsync<KycProfileDto>($"{_baseUrl}/kyc/me");
    }

    public async Task SubmitKycAsync(KycSubmitRequestDto dto)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/kyc/submit", dto);
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

    public async Task MarkAllNotificationsReadAsync()
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/notification/read-all", new { });
    }

    // Support Tickets
    public async Task<List<TicketDto>> GetOpenTicketsAsync()
    {
        return await _http.GetFromJsonAsync<List<TicketDto>>($"{_baseUrl}/support/tickets/open") ?? new();
    }

    // Support Tickets (Customer)
    public async Task<List<SupportTicketDto>> GetMySupportTicketsAsync()
    {
        return await _http.GetFromJsonAsync<List<SupportTicketDto>>($"{_baseUrl}/support/tickets/my") ?? new();
    }

    public async Task<TicketDetailResult> GetSupportTicketDetailAsync(Guid ticketId)
    {
        return (await _http.GetFromJsonAsync<TicketDetailResult>($"{_baseUrl}/support/tickets/{ticketId}"))!;
    }

    public async Task<SupportTicketDto> CreateSupportTicketAsync(CreateSupportTicketDto dto)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/support/tickets", dto);
        return (await response.Content.ReadFromJsonAsync<SupportTicketDto>())!;
    }

    public async Task<TicketMessageDto> AddTicketMessageAsync(Guid ticketId, AddTicketMessageDto dto)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/support/tickets/{ticketId}/messages", dto);
        return (await response.Content.ReadFromJsonAsync<TicketMessageDto>())!;
    }

    // Exchange
    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync()
    {
        return await _http.GetFromJsonAsync<List<ExchangeRateDto>>($"{_baseUrl}/exchange/rates") ?? new();
    }

    public async Task<ExchangeRateDetailDto> GetExchangeRateAsync(string from, string to)
    {
        return (await _http.GetFromJsonAsync<ExchangeRateDetailDto>($"{_baseUrl}/exchange/rate?from={from}&to={to}"))!;
    }

    public async Task<ExchangeQuoteDto> GetExchangeQuoteAsync(string from, string to, decimal amount)
    {
        return (await _http.GetFromJsonAsync<ExchangeQuoteDto>($"{_baseUrl}/exchange/quote?from={from}&to={to}&amount={amount}"))!;
    }

    public async Task ExecuteExchangeAsync(string fromWalletId, string toWalletId, decimal amount)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/exchange/execute", new { fromWalletId, toWalletId, amount });
    }

    // Audit Logs
    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int skip = 0, int take = 100)
    {
        return await _http.GetFromJsonAsync<List<AuditLogDto>>($"{_baseUrl}/audit?skip={skip}&take={take}") ?? new();
    }

    // Daily Cash Closing
    public async Task<BranchDailyClosingDto> GetBranchDailyClosingAsync(Guid branchId)
    {
        return (await _http.GetFromJsonAsync<BranchDailyClosingDto>($"{_baseUrl}/cash/branch/{branchId}/today"))!;
    }

    public async Task SubmitClosingAsync(Guid branchId, decimal actualClosing)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/cash/branch/{branchId}/close", new { actualClosing });
    }

    public async Task<ReconciliationResultDto> ReconcileClosingAsync(Guid closingId)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/cash/{closingId}/reconcile", new { });
        return (await response.Content.ReadFromJsonAsync<ReconciliationResultDto>())!;
    }

    public async Task<List<ClosingHistoryDto>> GetClosingHistoryAsync(Guid branchId)
    {
        return await _http.GetFromJsonAsync<List<ClosingHistoryDto>>($"{_baseUrl}/cash/branch/{branchId}/history") ?? new();
    }

    // QR Codes
    public async Task<QrCodeDto> GenerateQrAsync(string type, decimal? amount, string description)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/qr/generate", new { type, amount, description });
        return (await response.Content.ReadFromJsonAsync<QrCodeDto>())!;
    }

    public async Task<QrScanResult> ScanQrAsync(string qrCode)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/qr/scan", new { qrCode });
        return (await response.Content.ReadFromJsonAsync<QrScanResult>())!;
    }

    public async Task<object> PayQrAsync(Guid qrCodeId)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/qr/{qrCodeId}/pay", new { });
        return (await response.Content.ReadFromJsonAsync<object>())!;
    }

    public async Task<List<QrCodeDto>> GetMyQrCodesAsync()
    {
        return await _http.GetFromJsonAsync<List<QrCodeDto>>($"{_baseUrl}/qr/my") ?? new();
    }

    // Disputes
    public async Task<List<DisputeDto>> GetMyDisputesAsync()
    {
        return await _http.GetFromJsonAsync<List<DisputeDto>>($"{_baseUrl}/dispute/my") ?? new();
    }

    public async Task<DisputeDetailResult> GetDisputeDetailAsync(Guid disputeId)
    {
        return (await _http.GetFromJsonAsync<DisputeDetailResult>($"{_baseUrl}/dispute/{disputeId}"))!;
    }

    public async Task<DisputeDto> CreateDisputeAsync(CreateDisputeDto dto)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/dispute", dto);
        return (await response.Content.ReadFromJsonAsync<DisputeDto>())!;
    }

    public async Task<DisputeMessageDto> AddDisputeMessageAsync(Guid disputeId, AddDisputeMessageDto dto)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/dispute/{disputeId}/messages", dto);
        return (await response.Content.ReadFromJsonAsync<DisputeMessageDto>())!;
    }

    // Fraud Alerts
    public async Task<List<FraudAlertDto>> GetFraudAlertsAsync()
    {
        return await _http.GetFromJsonAsync<List<FraudAlertDto>>($"{_baseUrl}/fraud/alerts") ?? new();
    }

    public async Task UpdateFraudAlertStatusAsync(Guid alertId, string status)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/fraud/alerts/{alertId}/status", new { status });
    }

    public async Task AssignFraudAlertAsync(Guid alertId, string assignToEmail)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/fraud/alerts/{alertId}/assign", new { assignToEmail });
    }

    // Profile - Devices
    public async Task<List<DeviceDto>> GetDevicesAsync()
    {
        return await _http.GetFromJsonAsync<List<DeviceDto>>($"{_baseUrl}/auth/devices") ?? new();
    }

    public async Task RevokeDeviceAsync(Guid deviceId)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/auth/devices/{deviceId}/revoke", new { });
    }

    // Profile - Login History
    public async Task<List<LoginHistoryDto>> GetLoginHistoryAsync()
    {
        return await _http.GetFromJsonAsync<List<LoginHistoryDto>>($"{_baseUrl}/auth/login-history") ?? new();
    }

    // 2FA - These endpoints don't exist yet, marked as TODO
    public async Task<TwoFactorSetupDto> Setup2FAAsync()
    {
        var result = await _http.PostAsJsonAsync($"{_baseUrl}/auth/2fa/setup", new { });
        var content = await result.Content.ReadFromJsonAsync<TwoFactorSetupDto>();
        return content!;
    }

    public async Task VerifyAndEnable2FAAsync(string code)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/auth/2fa/verify-enable", new { code });
    }

    public async Task Disable2FAAsync()
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/auth/2fa/disable", new { });
    }

    // Transaction PIN
    public async Task SetTransactionPinAsync(string pin)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/auth/transaction-pin/set", new { pin });
    }

    public async Task<bool> VerifyTransactionPinAsync(string pin)
    {
        var result = await _http.PostAsJsonAsync($"{_baseUrl}/auth/transaction-pin/verify", new { Pin = pin });
        var content = await result.Content.ReadFromJsonAsync<PinVerifyResult>();
        return content?.Valid ?? false;
    }

    // Fee Quote
    public async Task<FeeQuoteResult> GetFeeQuoteAsync(decimal amount, string type, string currency = "NGN")
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/wallet/fee-quote", new { amount, type, currency });
        return (await response.Content.ReadFromJsonAsync<FeeQuoteResult>())!;
    }

    // Spending Analytics
    public async Task<SpendingAnalytics> GetSpendingAnalyticsAsync(Guid walletId, int months = 6)
    {
        return (await _http.GetFromJsonAsync<SpendingAnalytics>($"{_baseUrl}/transaction/wallet/{walletId}/analytics?months={months}"))!;
    }

    // Transaction Limits
    public async Task<TransactionLimitsResult> GetMyLimitsAsync()
    {
        return (await _http.GetFromJsonAsync<TransactionLimitsResult>($"{_baseUrl}/wallet/limits"))!;
    }

    // Statement Export - returns URL to download
    public string GetExportUrl(Guid walletId, DateTime? from = null, DateTime? to = null)
    {
        var url = $"{_baseUrl}/transaction/wallet/{walletId}/export";
        var params2 = new List<string>();
        if (from.HasValue) params2.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) params2.Add($"to={to.Value:yyyy-MM-dd}");
        if (params2.Count > 0) url += "?" + string.Join("&", params2);
        return url;
    }

    // Password Reset
    public async Task<string> ForgotPasswordAsync(string email)
    {
        var result = await _http.PostAsJsonAsync($"{_baseUrl}/auth/forgot-password", new { email });
        var content = await result.Content.ReadAsStringAsync();
        return content;
    }

    // WebAuthn
    public async Task<WebAuthnChallengeResult> GetWebAuthnChallengeAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/webauthn/challenge");
        return (await response.Content.ReadFromJsonAsync<WebAuthnChallengeResult>())!;
    }

    public async Task<WebAuthnChallengeResult> GetWebAuthnLoginChallengeAsync(string email)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/webauthn/login-challenge", new { email });
        return (await response.Content.ReadFromJsonAsync<WebAuthnChallengeResult>())!;
    }

    public async Task RegisterWebAuthnAsync(WebAuthnCredentialRequest credential)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/webauthn/register", credential);
    }

    public async Task<WebAuthnLoginResultResponse> VerifyWebAuthnAsync(WebAuthnVerifyRequest request)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/webauthn/verify", request);
        return (await response.Content.ReadFromJsonAsync<WebAuthnLoginResultResponse>())!;
    }

    public async Task<List<WebAuthnCredentialInfo>> GetMyWebAuthnCredentialsAsync()
    {
        return await _http.GetFromJsonAsync<List<WebAuthnCredentialInfo>>($"{_baseUrl}/webauthn/my") ?? new();
    }

    public async Task RemoveWebAuthnCredentialAsync(Guid id)
    {
        await _http.DeleteAsync($"{_baseUrl}/webauthn/{id}");
    }

    // Scheduled Transfers
    public async Task<List<ScheduledTransferInfo>> GetScheduledTransfersAsync()
    {
        return await _http.GetFromJsonAsync<List<ScheduledTransferInfo>>($"{_baseUrl}/scheduledtransfer") ?? new();
    }

    public async Task<ScheduledTransferInfo> CreateScheduledTransferAsync(CreateScheduledTransferRequest request)
    {
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/scheduledtransfer", request);
        return (await response.Content.ReadFromJsonAsync<ScheduledTransferInfo>())!;
    }

    public async Task PauseScheduledTransferAsync(Guid id)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/scheduledtransfer/{id}/pause", new { });
    }

    public async Task ResumeScheduledTransferAsync(Guid id)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/scheduledtransfer/{id}/resume", new { });
    }

    public async Task CancelScheduledTransferAsync(Guid id)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/scheduledtransfer/{id}/cancel", new { });
    }
}

public class TwoFactorSetupDto
{
    public string QrCodeUrl { get; set; } = "";
    public string ManualEntryKey { get; set; } = "";
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
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
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
    public string BranchId { get; set; } = string.Empty;
    public bool IsTransactionPinSet { get; set; }
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

public class ExchangeRateDetailDto
{
    public Guid Id { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExchangeQuoteDto
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal Fee { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class BranchDailyClosingDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal Adjustments { get; set; }
    public decimal ExpectedClosing { get; set; }
    public decimal ActualClosing { get; set; }
    public decimal Difference => Math.Abs(ActualClosing - ExpectedClosing);
    public bool IsClosed { get; set; }
    public bool Reconciled { get; set; }
}

public class ClosingHistoryDto
{
    public DateTime Date { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal ExpectedClosing { get; set; }
    public decimal ActualClosing { get; set; }
    public decimal Difference => Math.Abs(ActualClosing - ExpectedClosing);
    public bool Reconciled { get; set; }
}

public class ReconciliationResultDto
{
    public bool Reconciled { get; set; }
    public decimal Difference { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SupportTicketDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TicketMessageDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketDetailResult
{
    public SupportTicketDto Ticket { get; set; } = new();
    public List<TicketMessageDto> Messages { get; set; } = new();
}

public class CreateSupportTicketDto
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class AddTicketMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class QrCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class QrScanResult
{
    public bool IsValid { get; set; }
    public Guid? QrCodeId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class KycProfileDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class KycSubmitRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
}

public class FeeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AppliesTo { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public decimal MinFee { get; set; }
    public decimal MaxFee { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class LimitDto
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string LimitType { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class DisputeDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DisputeMessageDto
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DisputeDetailResult
{
    public DisputeDto Dispute { get; set; } = new();
    public List<DisputeMessageDto> Messages { get; set; } = new();
}

public class CreateDisputeDto
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class AddDisputeMessageDto
{
    public string Content { get; set; } = string.Empty;
}

public class FraudAlertDto
{
    public Guid Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LastActive { get; set; }
    public bool IsCurrent { get; set; }
}

public class LoginHistoryDto
{
    public Guid Id { get; set; }
    public DateTime LoginAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
}

public class PinVerifyResult
{
    public bool Valid { get; set; }
}

public class FeeQuoteResult
{
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class SpendingAnalytics
{
    public decimal TotalSpent { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal AverageTransaction { get; set; }
    public int TransactionCount { get; set; }
    public List<CategoryBreakdown> ByCategory { get; set; } = new();
    public List<MonthlySpending> MonthlyTrend { get; set; } = new();
}

public class CategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class MonthlySpending
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TransactionLimitsResult
{
    public List<LimitItemDto> Limits { get; set; } = new();
}

public class LimitItemDto
{
    public string Type { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public decimal Used { get; set; }
    public decimal Remaining { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class WebAuthnChallengeResult
{
    public string Challenge { get; set; } = string.Empty;
    public string RpId { get; set; } = string.Empty;
    public List<WebAuthnCredentialInfo> Credentials { get; set; } = new();
}

public class WebAuthnCredentialRequest
{
    public string CredentialId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int Counter { get; set; }
}

public class WebAuthnVerifyRequest
{
    public string CredentialId { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string ClientDataJSON { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class WebAuthnLoginResultResponse
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

public class WebAuthnCredentialInfo
{
    public Guid Id { get; set; }
    public string CredentialId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScheduledTransferInfo
{
    public Guid Id { get; set; }
    public string SourceWalletName { get; set; } = string.Empty;
    public string DestinationWalletName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RecurrenceType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public int ExecutionCount { get; set; }
    public int MaxExecutions { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateScheduledTransferRequest
{
    public Guid SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RecurrenceType { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public int MaxExecutions { get; set; } = 0;
}
