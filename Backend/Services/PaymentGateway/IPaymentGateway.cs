namespace ANpay.Api.Services.PaymentGateway;

public interface IPaymentGateway
{
    Task<GatewayResponse> ProcessPaymentAsync(PaymentRequest request);
    Task<GatewayResponse> VerifyPaymentAsync(string reference);
    Task<GatewayResponse> RefundPaymentAsync(string reference, decimal amount);
    Task<GatewayStatus> GetStatusAsync(string reference);
}

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class GatewayResponse
{
    public bool Success { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? RawResponse { get; set; }
}

public class GatewayStatus
{
    public string Reference { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded,
    Cancelled
}
