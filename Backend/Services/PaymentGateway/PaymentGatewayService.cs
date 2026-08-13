using ANpay.Api.Services.PaymentGateway;

namespace ANpay.Api.Services.PaymentGateway;

public class PaymentGatewayService
{
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(IEnumerable<IPaymentGateway> gateways, ILogger<PaymentGatewayService> logger)
    {
        _gateways = gateways;
        _logger = logger;
    }

    public async Task<GatewayResponse> ProcessPaymentAsync(PaymentRequest request, string? gatewayName = null)
    {
        var gateway = SelectGateway(request.Currency, gatewayName);
        _logger.LogInformation("Processing payment via {Gateway}: {Amount} {Currency}", gateway.GetType().Name, request.Amount, request.Currency);

        try
        {
            var response = await gateway.ProcessPaymentAsync(request);
            _logger.LogInformation("Payment {Reference} processed: {Success}", response.Reference, response.Success);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed for {Currency}", request.Currency);
            throw;
        }
    }

    public async Task<GatewayResponse> VerifyPaymentAsync(string reference, string? gatewayName = null)
    {
        var gateway = SelectGateway(null, gatewayName);
        _logger.LogInformation("Verifying payment {Reference} via {Gateway}", reference, gateway.GetType().Name);
        return await gateway.VerifyPaymentAsync(reference);
    }

    public async Task<GatewayResponse> RefundPaymentAsync(string reference, decimal amount, string? gatewayName = null)
    {
        var gateway = SelectGateway(null, gatewayName);
        _logger.LogInformation("Refunding {Amount} for {Reference} via {Gateway}", amount, reference, gateway.GetType().Name);
        return await gateway.RefundPaymentAsync(reference, amount);
    }

    public async Task<GatewayStatus> GetStatusAsync(string reference, string? gatewayName = null)
    {
        var gateway = SelectGateway(null, gatewayName);
        return await gateway.GetStatusAsync(reference);
    }

    private IPaymentGateway SelectGateway(string? currency, string? gatewayName)
    {
        if (!string.IsNullOrEmpty(gatewayName))
        {
            var named = _gateways.FirstOrDefault(g => g.GetType().Name.Contains(gatewayName, StringComparison.OrdinalIgnoreCase));
            if (named != null) return named;
        }

        if (!string.IsNullOrEmpty(currency) && currency.Equals("Crypto", StringComparison.OrdinalIgnoreCase))
        {
            return _gateways.LastOrDefault() ?? _gateways.First();
        }

        return _gateways.FirstOrDefault() ?? throw new InvalidOperationException("No payment gateway registered");
    }
}
