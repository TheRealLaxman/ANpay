namespace ANpay.Api.Services;

public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message);
    Task SendOtpAsync(string phoneNumber, string otp);
    Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency, string reference);
    Task SendLoginAlertAsync(string phoneNumber, string deviceInfo);
    Task SendLowBalanceAlertAsync(string phoneNumber, decimal balance, string currency);
    Task SendLoanRepaymentReminderAsync(string phoneNumber, decimal amount, DateTime dueDate);
}

public class ConsoleSmsService : ISmsService
{
    private readonly ILogger<ConsoleSmsService> _logger;

    public ConsoleSmsService(ILogger<ConsoleSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public Task SendOtpAsync(string phoneNumber, string otp)
    {
        _logger.LogInformation("[SMS] OTP to {Phone}: {Otp}", phoneNumber, otp);
        return Task.CompletedTask;
    }

    public Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency, string reference)
    {
        _logger.LogInformation("[SMS] Transaction to {Phone}: {Type} {Amount} {Currency} ref:{Ref}", phoneNumber, type, amount, currency, reference);
        return Task.CompletedTask;
    }

    public Task SendLoginAlertAsync(string phoneNumber, string deviceInfo)
    {
        _logger.LogInformation("[SMS] Login alert to {Phone}: {Device}", phoneNumber, deviceInfo);
        return Task.CompletedTask;
    }

    public Task SendLowBalanceAlertAsync(string phoneNumber, decimal balance, string currency)
    {
        _logger.LogInformation("[SMS] Low balance to {Phone}: {Balance} {Currency}", phoneNumber, balance, currency);
        return Task.CompletedTask;
    }

    public Task SendLoanRepaymentReminderAsync(string phoneNumber, decimal amount, DateTime dueDate)
    {
        _logger.LogInformation("[SMS] Loan reminder to {Phone}: {Amount} due {Date}", phoneNumber, amount, dueDate);
        return Task.CompletedTask;
    }
}

public class TwilioSmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IConfiguration config, ILogger<TwilioSmsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message)
    {
        try
        {
            var accountSid = _config["Twilio:AccountSid"];
            var authToken = _config["Twilio:AuthToken"];
            var fromNumber = _config["Twilio:FromNumber"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
            {
                _logger.LogWarning("Twilio not configured. SMS to {Phone} was not sent. Configure Twilio:AccountSid and Twilio:AuthToken in appsettings.", phoneNumber);
                return;
            }

            using var client = new System.Net.Http.HttpClient();
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var content = new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "To", phoneNumber },
                { "From", fromNumber ?? "" },
                { "Body", message }
            });

            var response = await client.PostAsync($"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent to {Phone}: {Status}", phoneNumber, response.StatusCode);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("SMS failed to {Phone}: {Status} - {Error}", phoneNumber, response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {Phone}", phoneNumber);
        }
    }

    public Task SendOtpAsync(string phoneNumber, string otp)
    {
        return SendAsync(phoneNumber, $"Your ANpay verification code is: {otp}. Valid for 10 minutes. Do not share this code.");
    }

    public Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency, string reference)
    {
        return SendAsync(phoneNumber, $"ANpay Alert: {type} of {currency} {amount:N2} processed. Ref: {reference}");
    }

    public Task SendLoginAlertAsync(string phoneNumber, string deviceInfo)
    {
        return SendAsync(phoneNumber, $"ANpay Security: New login detected from {deviceInfo}. If this wasn't you, change your password immediately.");
    }

    public Task SendLowBalanceAlertAsync(string phoneNumber, decimal balance, string currency)
    {
        return SendAsync(phoneNumber, $"ANpay Alert: Your wallet balance is low ({currency} {balance:N2}). Top up to continue transacting.");
    }

    public Task SendLoanRepaymentReminderAsync(string phoneNumber, decimal amount, DateTime dueDate)
    {
        return SendAsync(phoneNumber, $"ANpay Loan Reminder: You have a repayment of NGN {amount:N2} due on {dueDate:MMM dd, yyyy}. Ensure sufficient wallet balance.");
    }
}
