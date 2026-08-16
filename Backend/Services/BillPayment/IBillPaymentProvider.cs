namespace ANpay.Api.Services.BillPaymentProvider;

public interface IBillPaymentProvider
{
    Task<BillPaymentResponse> ValidateAsync(BillPaymentRequest request);
    Task<BillPaymentResponse> PayAsync(BillPaymentRequest request);
    Task<BillPaymentStatusResponse> CheckStatusAsync(string reference);
}

public class BillPaymentRequest
{
    public string ProviderCode { get; set; } = string.Empty;
    public string BillerCode { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string? Description { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BillPaymentResponse
{
    public bool Success { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? Token { get; set; }
    public string? Pin { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class BillPaymentStatusResponse
{
    public string Reference { get; set; } = string.Empty;
    public BillProviderPaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public enum BillProviderPaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded
}
