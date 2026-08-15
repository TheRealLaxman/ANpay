using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace ANpay.Api.Middleware;

public class AccountLockoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccountLockoutMiddleware> _logger;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockoutDuration;
    private static readonly ConcurrentDictionary<string, FailedLoginTracker> _failedAttempts = new();

    public AccountLockoutMiddleware(RequestDelegate next, ILogger<AccountLockoutMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxFailedAttempts = configuration.GetValue("AccountLockout:MaxFailedAttempts", 5);
        _lockoutDuration = TimeSpan.FromMinutes(configuration.GetValue("AccountLockout:LockoutDurationMinutes", 15));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsLoginEndpoint(context))
        {
            var email = await ExtractEmailFromRequest(context);
            if (!string.IsNullOrEmpty(email))
            {
                var normalizedEmail = email.ToLowerInvariant().Trim();

                if (IsAccountLocked(normalizedEmail))
                {
                    var tracker = _failedAttempts[normalizedEmail];
                    var remainingTime = tracker.LockoutEnd - DateTime.UtcNow;
                    remainingTime = remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;

                    _logger.LogWarning("Locked out account attempted login: {Email}. Remaining: {Remaining}",
                        email, remainingTime);

                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.ContentType = "application/json";

                    var response = new
                    {
                        success = false,
                        message = $"Account locked due to too many failed attempts. Try again in {remainingTime.Minutes + 1} minutes.",
                        statusCode = 403,
                        lockoutEnd = tracker.LockoutEnd,
                        retryAfterSeconds = (int)remainingTime.TotalSeconds
                    };

                    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await context.Response.WriteAsync(json);
                    return;
                }
            }
        }

        var originalBodyStream = context.Response.Body;

        using var newBodyStream = new MemoryStream();
        context.Response.Body = newBodyStream;

        await _next(context);

        newBodyStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(newBodyStream).ReadToEndAsync();
        newBodyStream.Seek(0, SeekOrigin.Begin);
        await newBodyStream.CopyToAsync(originalBodyStream);

        // Record failed attempt on 401/403 login responses
        if (IsLoginEndpoint(context) && (context.Response.StatusCode == 401 || context.Response.StatusCode == 403))
        {
            if (await ExtractEmailFromRequest(context) is { } loginEmail)
            {
                var normalizedEmail = loginEmail.ToLowerInvariant().Trim();
                RecordFailedAttempt(normalizedEmail);
            }
        }

        // Reset on successful login
        if (IsLoginEndpoint(context) && context.Response.StatusCode == 200)
        {
            if (await ExtractEmailFromRequest(context) is { } loginEmail)
            {
                var normalizedEmail = loginEmail.ToLowerInvariant().Trim();
                ResetFailedAttempts(normalizedEmail);
            }
        }
    }

    private bool IsLoginEndpoint(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = context.Request.Method;

        return method == "POST" && (path.Contains("/api/auth/login") || path.Contains("/auth/login"));
    }

    private async Task<string?> ExtractEmailFromRequest(HttpContext context)
    {
        try
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body)) return null;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract email from login request");
        }

        return null;
    }

    private bool IsAccountLocked(string normalizedEmail)
    {
        if (!_failedAttempts.TryGetValue(normalizedEmail, out var tracker))
            return false;

        lock (tracker.Lock)
        {
            if (tracker.Attempts >= _maxFailedAttempts && tracker.LockoutEnd > DateTime.UtcNow)
                return true;

            if (tracker.Attempts >= _maxFailedAttempts && tracker.LockoutEnd <= DateTime.UtcNow)
            {
                tracker.Attempts = 0;
                tracker.LockoutEnd = DateTime.MinValue;
                return false;
            }

            return false;
        }
    }

    public static void RecordFailedAttempt(string email)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var tracker = _failedAttempts.GetOrAdd(normalizedEmail, _ => new FailedLoginTracker());

        lock (tracker.Lock)
        {
            if (tracker.LockoutEnd > DateTime.UtcNow)
                return;

            tracker.Attempts++;

            if (tracker.Attempts >= 5) // default max
            {
                tracker.LockoutEnd = DateTime.UtcNow.AddMinutes(15); // default lockout
            }
        }
    }

    public static void ResetFailedAttempts(string normalizedEmail)
    {
        if (_failedAttempts.TryGetValue(normalizedEmail, out var tracker))
        {
            lock (tracker.Lock)
            {
                tracker.Attempts = 0;
                tracker.LockoutEnd = DateTime.MinValue;
            }
        }
    }

    private class FailedLoginTracker
    {
        public object Lock { get; } = new();
        public int Attempts { get; set; }
        public DateTime LockoutEnd { get; set; }
    }
}
