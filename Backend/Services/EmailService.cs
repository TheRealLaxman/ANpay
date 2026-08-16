using System.Net;
using System.Net.Mail;

namespace ANpay.Api.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, bool isHtml = true);
    Task SendWelcomeAsync(string to, string firstName);
    Task SendTransactionReceiptAsync(string to, string firstName, string type, decimal amount, string currency, string reference);
    Task SendKycStatusAsync(string to, string firstName, bool approved, string notes);
    Task SendOtpAsync(string to, string otp);
    Task SendPasswordResetAsync(string to, string resetLink);
    Task SendLowBalanceAlertAsync(string to, string firstName, decimal balance, string currency);
    Task SendLoginAlertAsync(string to, string firstName, string deviceInfo, string ipAddress);
    Task SendLoanRepaymentReminderAsync(string to, string firstName, decimal amount, DateTime dueDate);
}

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            var smtpSection = _config.GetSection("Smtp");
            var host = smtpSection["Host"] ?? "localhost";
            var port = int.Parse(smtpSection["Port"] ?? "587");
            var username = smtpSection["Username"] ?? "";
            var password = smtpSection["Password"] ?? "";
            var fromEmail = smtpSection["FromEmail"] ?? "noreply@anpay.com";
            var fromName = smtpSection["FromName"] ?? "ANpay";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("SMTP not configured. Email to {To} with subject '{Subject}' was not sent. Configure Smtp:Username and Smtp:Password in appsettings.", to, subject);
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = bool.TryParse(smtpSection["EnableSsl"], out var ssl) ? ssl : true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };
            message.To.Add(to);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }

    public Task SendWelcomeAsync(string to, string firstName)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'>Welcome to <span style='color: #ef4444;'>AN</span>pay</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff;'>
                    <h2>Welcome, {firstName}!</h2>
                    <p>Your account has been created successfully. You can now start using ANpay to manage your digital wallet.</p>
                    <p>Get started by creating your first wallet and making a deposit.</p>
                    <br/>
                    <p>Best regards,<br/>ANpay Team</p>
                </div>
                <div style='background: #0f0f23; color: #666; padding: 15px; text-align: center; font-size: 12px;'>
                    <p>ANpay Digital Wallet Ecosystem</p>
                </div>
            </div>";
        return SendAsync(to, "Welcome to ANpay", body);
    }

    public Task SendTransactionReceiptAsync(string to, string firstName, string type, decimal amount, string currency, string reference)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Transaction Receipt</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff;'>
                    <h2>Transaction {type}</h2>
                    <table style='width: 100%; border-collapse: collapse;'>
                        <tr><td style='padding: 8px 0; color: #888;'>Type</td><td style='padding: 8px 0;'>{type}</td></tr>
                        <tr><td style='padding: 8px 0; color: #888;'>Amount</td><td style='padding: 8px 0; font-weight: bold; color: #22c55e;'>{currency} {amount:N2}</td></tr>
                        <tr><td style='padding: 8px 0; color: #888;'>Reference</td><td style='padding: 8px 0;'>{reference}</td></tr>
                        <tr><td style='padding: 8px 0; color: #888;'>Date</td><td style='padding: 8px 0;'>{DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</td></tr>
                    </table>
                    <br/>
                    <p>Dear {firstName}, your transaction has been processed successfully.</p>
                </div>
                <div style='background: #0f0f23; color: #666; padding: 15px; text-align: center; font-size: 12px;'>
                    <p>ANpay Digital Wallet Ecosystem</p>
                </div>
            </div>";
        return SendAsync(to, $"ANpay - {type} Receipt", body);
    }

    public Task SendKycStatusAsync(string to, string firstName, bool approved, string notes)
    {
        var status = approved ? "Approved" : "Rejected";
        var color = approved ? "#22c55e" : "#ef4444";
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay KYC Update</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff;'>
                    <h2 style='color: {color};'>KYC {status}</h2>
                    <p>Dear {firstName},</p>
                    <p>Your KYC verification has been <strong>{status.ToLower()}</strong>.</p>
                    {(string.IsNullOrEmpty(notes) ? "" : $"<p><strong>Notes:</strong> {notes}</p>")}
                    <br/>
                    <p>Best regards,<br/>ANpay Team</p>
                </div>
            </div>";
        return SendAsync(to, $"ANpay - KYC {status}", body);
    }

    public Task SendOtpAsync(string to, string otp)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Verification</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff; text-align: center;'>
                    <h2>Your OTP Code</h2>
                    <div style='background: #0f0f23; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                        <span style='font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #ef4444;'>{otp}</span>
                    </div>
                    <p style='color: #888;'>This code expires in 10 minutes.</p>
                    <p style='color: #888;'>Do not share this code with anyone.</p>
                </div>
            </div>";
        return SendAsync(to, "ANpay - Your OTP Code", body);
    }

    public Task SendPasswordResetAsync(string to, string resetLink)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Password Reset</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff; text-align: center;'>
                    <h2>Reset Your Password</h2>
                    <p>Click the button below to reset your password.</p>
                    <a href='{resetLink}' style='display: inline-block; background: #ef4444; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0;'>Reset Password</a>
                    <p style='color: #888;'>This link expires in 1 hour.</p>
                    <p style='color: #888;'>If you didn't request this, ignore this email.</p>
                </div>
            </div>";
        return SendAsync(to, "ANpay - Password Reset", body);
    }

    public Task SendLowBalanceAlertAsync(string to, string firstName, decimal balance, string currency)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Low Balance Alert</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff; text-align: center;'>
                    <h2 style='color: #f59e0b;'>Low Balance Warning</h2>
                    <p>Dear {firstName},</p>
                    <p>Your wallet balance is running low.</p>
                    <div style='background: #0f0f23; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                        <span style='font-size: 24px; font-weight: bold; color: #f59e0b;'>{currency} {balance:N2}</span>
                    </div>
                    <p>Consider topping up your wallet to continue enjoying seamless transactions.</p>
                </div>
            </div>";
        return SendAsync(to, "ANpay - Low Balance Alert", body);
    }

    public Task SendLoginAlertAsync(string to, string firstName, string deviceInfo, string ipAddress)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Security Alert</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff; text-align: center;'>
                    <h2 style='color: #22c55e;'>New Login Detected</h2>
                    <p>Dear {firstName},</p>
                    <p>A new login was detected on your account.</p>
                    <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                        <tr><td style='padding: 8px 0; color: #888;'>Device</td><td style='padding: 8px 0;'>{deviceInfo}</td></tr>
                        <tr><td style='padding: 8px 0; color: #888;'>IP Address</td><td style='padding: 8px 0;'>{ipAddress}</td></tr>
                        <tr><td style='padding: 8px 0; color: #888;'>Time</td><td style='padding: 8px 0;'>{DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</td></tr>
                    </table>
                    <p>If this wasn't you, please change your password immediately.</p>
                </div>
            </div>";
        return SendAsync(to, "ANpay - Security Alert: New Login", body);
    }

    public Task SendLoanRepaymentReminderAsync(string to, string firstName, decimal amount, DateTime dueDate)
    {
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: #000; color: #fff; padding: 20px; text-align: center;'>
                    <h1 style='margin: 0;'><span style='color: #ef4444;'>AN</span>pay Loan Reminder</h1>
                </div>
                <div style='padding: 30px; background: #1a1a2e; color: #fff; text-align: center;'>
                    <h2 style='color: #f59e0b;'>Repayment Due Soon</h2>
                    <p>Dear {firstName},</p>
                    <p>You have a loan repayment due soon.</p>
                    <div style='background: #0f0f23; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                        <p style='color: #888; margin: 0;'>Amount Due</p>
                        <span style='font-size: 24px; font-weight: bold; color: #ef4444;'>NGN {amount:N2}</span>
                        <p style='color: #888; margin: 10px 0 0;'>Due Date: {dueDate:MMM dd, yyyy}</p>
                    </div>
                    <p>Ensure your wallet has sufficient balance for auto-debit.</p>
                </div>
            </div>";
        return SendAsync(to, "ANpay - Loan Repayment Reminder", body);
    }
}

public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, bool isHtml = true)
    {
        _logger.LogInformation("[EMAIL] To: {To} | Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }

    public Task SendWelcomeAsync(string to, string firstName)
    {
        _logger.LogInformation("[EMAIL] Welcome to {Name} at {To}", firstName, to);
        return Task.CompletedTask;
    }

    public Task SendTransactionReceiptAsync(string to, string firstName, string type, decimal amount, string currency, string reference)
    {
        _logger.LogInformation("[EMAIL] Receipt to {To}: {Type} {Amount} {Currency} ref:{Ref}", to, type, amount, currency, reference);
        return Task.CompletedTask;
    }

    public Task SendKycStatusAsync(string to, string firstName, bool approved, string notes)
    {
        _logger.LogInformation("[EMAIL] KYC {Status} for {Name} at {To}", approved ? "Approved" : "Rejected", firstName, to);
        return Task.CompletedTask;
    }

    public Task SendOtpAsync(string to, string otp)
    {
        _logger.LogInformation("[EMAIL] OTP to {To}: {Otp}", to, otp);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string to, string resetLink)
    {
        _logger.LogInformation("[EMAIL] Password reset to {To}", to);
        return Task.CompletedTask;
    }

    public Task SendLowBalanceAlertAsync(string to, string firstName, decimal balance, string currency)
    {
        _logger.LogInformation("[EMAIL] Low balance alert to {To}: {Balance} {Currency}", to, balance, currency);
        return Task.CompletedTask;
    }

    public Task SendLoginAlertAsync(string to, string firstName, string deviceInfo, string ipAddress)
    {
        _logger.LogInformation("[EMAIL] Login alert to {To}: {Device} from {IP}", to, deviceInfo, ipAddress);
        return Task.CompletedTask;
    }

    public Task SendLoanRepaymentReminderAsync(string to, string firstName, decimal amount, DateTime dueDate)
    {
        _logger.LogInformation("[EMAIL] Loan reminder to {To}: {Amount} due {Date}", to, amount, dueDate);
        return Task.CompletedTask;
    }
}
