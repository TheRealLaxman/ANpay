using System.Collections.Concurrent;
using System.Text.Json;

namespace ANpay.Api.Services;

public class MarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketDataService> _logger;
    private readonly string _ratesUrl;

    private static readonly ConcurrentDictionary<string, MarketRate> _cachedRates = new();
    private static readonly ConcurrentDictionary<string, List<RateHistory>> _rateHistory = new();
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly object _fetchLock = new();
    private const int CacheDurationSeconds = 60;
    private const int HistoryRetentionHours = 24;

    public MarketDataService(HttpClient httpClient, ILogger<MarketDataService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ratesUrl = configuration["MarketData:RatesUrl"] ?? "https://api.example.com/market/rates";
    }

    public async Task<List<MarketRate>> GetAllRatesAsync()
    {
        await RefreshCacheIfNeeded();
        return _cachedRates.Values.ToList();
    }

    public async Task<MarketRate?> GetRateAsync(string from, string to)
    {
        await RefreshCacheIfNeeded();
        var key = $"{from.ToUpper()}/{to.ToUpper()}";
        _cachedRates.TryGetValue(key, out var rate);
        return rate;
    }

    public async Task<List<RateHistory>> GetRateHistoryAsync(string from, string to)
    {
        var key = $"{from.ToUpper()}/{to.ToUpper()}";
        if (_rateHistory.TryGetValue(key, out var history))
        {
            var cutoff = DateTime.UtcNow.AddHours(-HistoryRetentionHours);
            return history.Where(h => h.Timestamp >= cutoff).ToList();
        }
        return new List<RateHistory>();
    }

    private async Task RefreshCacheIfNeeded()
    {
        if ((DateTime.UtcNow - _lastFetch).TotalSeconds < CacheDurationSeconds)
            return;

        lock (_fetchLock)
        {
            if ((DateTime.UtcNow - _lastFetch).TotalSeconds < CacheDurationSeconds)
                return;
        }

        try
        {
            _logger.LogInformation("Fetching market rates from {Url}", _ratesUrl);
            var response = await _httpClient.GetAsync(_ratesUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var rates = JsonSerializer.Deserialize<List<MarketRate>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (rates != null)
            {
                foreach (var rate in rates)
                {
                    var key = $"{rate.From}/{rate.To}";
                    _cachedRates[key] = rate;
                    RecordHistory(key, rate);
                }
                _lastFetch = DateTime.UtcNow;
                _logger.LogInformation("Cached {Count} market rates", rates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market rates");
        }
    }

    private static void RecordHistory(string key, MarketRate rate)
    {
        _rateHistory.AddOrUpdate(key,
            _ => new List<RateHistory>
            {
                new() { Rate = rate.Rate, Timestamp = DateTime.UtcNow }
            },
            (_, list) =>
            {
                var cutoff = DateTime.UtcNow.AddHours(-HistoryRetentionHours);
                list.RemoveAll(h => h.Timestamp < cutoff);
                list.Add(new RateHistory { Rate = rate.Rate, Timestamp = DateTime.UtcNow });
                return list;
            });
    }
}

public class MarketRate
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal? High24h { get; set; }
    public decimal? Low24h { get; set; }
    public decimal? Volume24h { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class RateHistory
{
    public decimal Rate { get; set; }
    public DateTime Timestamp { get; set; }
}
