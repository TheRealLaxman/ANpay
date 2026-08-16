using System.Collections.Concurrent;
using System.Net;

namespace ANpay.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _maxRequestsPerMinute;
    private readonly int _cleanupIntervalSeconds;
    private static readonly ConcurrentDictionary<string, RequestTracker> _ipTrackers = new();
    private static Timer? _cleanupTimer;
    private static readonly HashSet<string> _skippedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/health/live",
        "/health/ready",
        "/api/health"
    };

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxRequestsPerMinute = configuration.GetValue("RateLimiting:MaxRequestsPerMinute", 60);
        _cleanupIntervalSeconds = configuration.GetValue("RateLimiting:CleanupIntervalSeconds", 30);

        if (_cleanupTimer == null)
        {
            var interval = TimeSpan.FromSeconds(_cleanupIntervalSeconds);
            _cleanupTimer = new Timer(CleanupOldEntries, null, interval, interval);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSkippedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var ipAddress = GetClientIpAddress(context);
        var tracker = _ipTrackers.GetOrAdd(ipAddress, _ => new RequestTracker());

        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-60);
        int retryAfter = 0;

        lock (tracker.Lock)
        {
            tracker.Requests.RemoveAll(r => r < windowStart);

            if (tracker.Requests.Count >= _maxRequestsPerMinute)
            {
                var oldestRequest = tracker.Requests.First();
                retryAfter = (int)(oldestRequest.AddSeconds(60) - now).TotalSeconds + 1;
                retryAfter = Math.Max(retryAfter, 1);

                _logger.LogWarning("Rate limit exceeded for IP {IpAddress}. Requests: {Count}/{Max}",
                    ipAddress, tracker.Requests.Count, _maxRequestsPerMinute);
            }
            else
            {
                tracker.Requests.Add(now);
            }
        }

        if (retryAfter > 0)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Too many requests. Please try again later.",
                statusCode = 429,
                retryAfter
            };

            var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }

    private static bool IsSkippedPath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        return _skippedPaths.Any(sp => pathValue.StartsWith(sp, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Only trust forwarded headers behind a reverse proxy
        // In production, configure known proxy IPs and use the last trusted hop
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // If behind a reverse proxy, the RemoteIpAddress will be the proxy
        // Only use X-Forwarded-For when RemoteIpAddress is a loopback address
        if (context.Connection.RemoteIpAddress != null && System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first (original client) IP, not the last
                var ip = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(ip)) return ip;
            }
        }

        return remoteIp;
    }

    private static void CleanupOldEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var keysToRemove = _ipTrackers
            .Where(kvp =>
            {
                lock (kvp.Value.Lock)
                {
                    return kvp.Value.Requests.All(r => r < cutoff);
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _ipTrackers.TryRemove(key, out _);
        }
    }

    private class RequestTracker
    {
        public object Lock { get; } = new();
        public List<DateTime> Requests { get; } = new();
    }
}
