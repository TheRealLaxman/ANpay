using ANpay.Api.Services.PaymentGateway;

namespace ANpay.Api.Services.PaymentGateway;

public class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<GatewayResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("MockGateway: Processing payment of {Amount} {Currency}", request.Amount, request.Currency);

        var delay = Random.Shared.Next(1000, 2500);
        await Task.Delay(delay);

        var success = request.Amount < 1000 || Random.Shared.Next(100) < 90;

        var reference = $"MOCK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";

        var response = new GatewayResponse
        {
            Success = success,
            Reference = reference,
            Amount = request.Amount,
            Currency = request.Currency,
            ProcessedAt = DateTime.UtcNow,
            Message = success ? "Payment processed successfully" : "Payment declined by bank",
            RawResponse = System.Text.Json.JsonSerializer.Serialize(new
            {
                mock = true,
                processingTimeMs = delay,
                errorCode = success ? null : "DECLINED"
            })
        };

        _logger.LogInformation("MockGateway: Payment {Reference} - {Status}", reference, success ? "Success" : "Failed");
        return response;
    }

    public async Task<GatewayResponse> VerifyPaymentAsync(string reference)
    {
        _logger.LogInformation("MockGateway: Verifying payment {Reference}", reference);
        await Task.Delay(500);

        return new GatewayResponse
        {
            Success = true,
            Reference = reference,
            Message = "Payment verified",
            ProcessedAt = DateTime.UtcNow
        };
    }

    public async Task<GatewayResponse> RefundPaymentAsync(string reference, decimal amount)
    {
        _logger.LogInformation("MockGateway: Refunding {Amount} for {Reference}", amount, reference);
        await Task.Delay(800);

        return new GatewayResponse
        {
            Success = true,
            Reference = $"REFUND-{reference}",
            Amount = amount,
            Message = "Refund processed successfully",
            ProcessedAt = DateTime.UtcNow
        };
    }

    public async Task<GatewayStatus> GetStatusAsync(string reference)
    {
        _logger.LogInformation("MockGateway: Checking status for {Reference}", reference);
        await Task.Delay(300);

        return new GatewayStatus
        {
            Reference = reference,
            Status = PaymentStatus.Completed,
            Amount = 100m,
            Currency = "USD",
            LastUpdated = DateTime.UtcNow
        };
    }
}
