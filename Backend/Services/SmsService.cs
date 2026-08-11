namespace ANpay.Api.Services;

public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message);
    Task SendOtpAsync(string phoneNumber, string otp);
    Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency);
    Task SendLoginAlertAsync(string phoneNumber, string deviceInfo);
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

    public Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency)
    {
        _logger.LogInformation("[SMS] Transaction to {Phone}: {Type} {Amount} {Currency}", phoneNumber, type, amount, currency);
        return Task.CompletedTask;
    }

    public Task SendLoginAlertAsync(string phoneNumber, string deviceInfo)
    {
        _logger.LogInformation("[SMS] Login alert to {Phone}: {Device}", phoneNumber, deviceInfo);
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
                _logger.LogWarning("Twilio not configured, falling back to console");
                _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", phoneNumber, message);
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
            _logger.LogInformation("SMS sent to {Phone}: {Status}", phoneNumber, response.StatusCode);
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

    public Task SendTransactionAlertAsync(string phoneNumber, string type, decimal amount, string currency)
    {
        return SendAsync(phoneNumber, $"ANpay Alert: {type} of {currency} {amount:N2} processed. Ref: {DateTime.UtcNow:yyyyMMddHHmmss}");
    }

    public Task SendLoginAlertAsync(string phoneNumber, string deviceInfo)
    {
        return SendAsync(phoneNumber, $"ANpay Security: New login detected from {deviceInfo}. If this wasn't you, change your password immediately.");
    }
}
