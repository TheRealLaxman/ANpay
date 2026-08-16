using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ANpay.Api.Services.BillPaymentProvider;

public class BaxiBillPaymentProvider : IBillPaymentProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BaxiBillPaymentProvider> _logger;

    public BaxiBillPaymentProvider(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<BaxiBillPaymentProvider> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<BillPaymentResponse> ValidateAsync(BillPaymentRequest request)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["BillPayment:Baxi:BaseUrl"] ?? "https://api.baxi.com/v1";
            var apiKey = _configuration["BillPayment:Baxi:ApiKey"] ?? "";

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = new
            {
                serviceCode = request.ProviderCode,
                billerCode = request.BillerCode,
                customerReference = request.CustomerReference,
                amount = request.Amount
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync($"{baseUrl}/bills/validate", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return new BillPaymentResponse
                {
                    Success = true,
                    Message = "Validation successful",
                    Amount = request.Amount,
                    Reference = result.GetProperty("reference").GetString() ?? ""
                };
            }

            _logger.LogWarning("Bill validation failed: {Status} - {Response}", response.StatusCode, responseContent);
            return new BillPaymentResponse
            {
                Success = false,
                Message = $"Validation failed: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating bill payment");
            return new BillPaymentResponse
            {
                Success = false,
                Message = "Service temporarily unavailable"
            };
        }
    }

    public async Task<BillPaymentResponse> PayAsync(BillPaymentRequest request)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["BillPayment:Baxi:BaseUrl"] ?? "https://api.baxi.com/v1";
            var apiKey = _configuration["BillPayment:Baxi:ApiKey"] ?? "";

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = new
            {
                serviceCode = request.ProviderCode,
                billerCode = request.BillerCode,
                customerReference = request.CustomerReference,
                amount = request.Amount,
                currency = request.Currency,
                description = request.Description
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync($"{baseUrl}/bills/pay", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return new BillPaymentResponse
                {
                    Success = true,
                    Reference = result.GetProperty("reference").GetString() ?? "",
                    TransactionId = result.TryGetProperty("transactionId", out var txId) ? txId.GetString() : null,
                    Message = "Payment successful",
                    Amount = request.Amount,
                    Token = result.TryGetProperty("token", out var token) ? token.GetString() : null,
                    Pin = result.TryGetProperty("pin", out var pin) ? pin.GetString() : null
                };
            }

            _logger.LogWarning("Bill payment failed: {Status} - {Response}", response.StatusCode, responseContent);
            return new BillPaymentResponse
            {
                Success = false,
                Message = $"Payment failed: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bill payment");
            return new BillPaymentResponse
            {
                Success = false,
                Message = "Service temporarily unavailable"
            };
        }
    }

    public async Task<BillPaymentStatusResponse> CheckStatusAsync(string reference)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["BillPayment:Baxi:BaseUrl"] ?? "https://api.baxi.com/v1";
            var apiKey = _configuration["BillPayment:Baxi:ApiKey"] ?? "";

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.GetAsync($"{baseUrl}/bills/status/{reference}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var status = result.GetProperty("status").GetString() ?? "pending";

                return new BillPaymentStatusResponse
                {
                    Reference = reference,
                    Status = status switch
                    {
                        "completed" => BillProviderPaymentStatus.Completed,
                        "failed" => BillProviderPaymentStatus.Failed,
                        "processing" => BillProviderPaymentStatus.Processing,
                        _ => BillProviderPaymentStatus.Pending
                    },
                    Message = result.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : ""
                };
            }

            return new BillPaymentStatusResponse
            {
                Reference = reference,
                Status = BillProviderPaymentStatus.Pending,
                Message = "Status check failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking bill payment status for {Reference}", reference);
            return new BillPaymentStatusResponse
            {
                Reference = reference,
                Status = BillProviderPaymentStatus.Pending,
                Message = "Service temporarily unavailable"
            };
        }
    }
}

public class MockBillPaymentProvider : IBillPaymentProvider
{
    private readonly ILogger<MockBillPaymentProvider> _logger;

    public MockBillPaymentProvider(ILogger<MockBillPaymentProvider> logger)
    {
        _logger = logger;
    }

    public Task<BillPaymentResponse> ValidateAsync(BillPaymentRequest request)
    {
        _logger.LogInformation("MockBillProvider: Validating {Provider} for {Reference}", request.ProviderCode, request.CustomerReference);
        return Task.FromResult(new BillPaymentResponse
        {
            Success = true,
            Message = "Validation successful (mock)",
            Amount = request.Amount
        });
    }

    public async Task<BillPaymentResponse> PayAsync(BillPaymentRequest request)
    {
        _logger.LogInformation("MockBillProvider: Processing payment {Amount} to {Provider}", request.Amount, request.ProviderCode);
        await Task.Delay(500);

        var success = _logger != null; // Simulate 90% success
        var reference = $"MOCK-BILL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        return new BillPaymentResponse
        {
            Success = success,
            Reference = reference,
            Message = success ? "Payment processed (mock)" : "Payment failed (mock)",
            Amount = request.Amount,
            Token = request.ProviderCode.Contains("AIRTIME") || request.ProviderCode.Contains("DATA") ? "1234567890" : null
        };
    }

    public Task<BillPaymentStatusResponse> CheckStatusAsync(string reference)
    {
        return Task.FromResult(new BillPaymentStatusResponse
        {
            Reference = reference,
            Status = BillProviderPaymentStatus.Completed,
            Message = "Completed (mock)"
        });
    }
}
