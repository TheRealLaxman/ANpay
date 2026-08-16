using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;

namespace ANpay.Api.Workers;

public class WebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookDeliveryWorker(
        IServiceProvider serviceProvider,
        ILogger<WebhookDeliveryWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var pendingDeliveries = await context.WebhookDeliveries
                    .Include(wd => wd.Webhook)
                    .Where(wd => wd.Status == WebhookDeliveryStatus.Pending
                                && wd.AttemptNumber < (wd.Webhook.RetryCount + 1))
                    .OrderBy(wd => wd.CreatedAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var delivery in pendingDeliveries)
                {
                    await ProcessDeliveryAsync(delivery, context, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook deliveries");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("WebhookDeliveryWorker stopped");
    }

    private async Task ProcessDeliveryAsync(WebhookDelivery delivery, ApplicationDbContext context, CancellationToken ct)
    {
        if (delivery.Webhook == null || !delivery.Webhook.IsActive)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            await context.SaveChangesAsync(ct);
            return;
        }

        delivery.AttemptNumber++;
        delivery.LastAttemptAt = DateTime.UtcNow;

        // Add exponential backoff delay between retries
        if (delivery.AttemptNumber > 1)
        {
            var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, delivery.AttemptNumber - 1) * 5);
            if (backoffDelay > TimeSpan.FromMinutes(5)) backoffDelay = TimeSpan.FromMinutes(5);
            await Task.Delay(backoffDelay, ct);
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(delivery.Webhook.TimeoutSeconds);

            var payload = JsonSerializer.Serialize(new
            {
                eventType = delivery.EventType,
                data = delivery.Payload,
                timestamp = DateTime.UtcNow,
                deliveryId = delivery.Id
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(delivery.Webhook.Secret))
            {
                var signature = ComputeHmacSha256(payload, delivery.Webhook.Secret);
                content.Headers.Add("X-Webhook-Signature", signature);
            }

            content.Headers.Add("X-Webhook-Event", delivery.EventType);
            content.Headers.Add("X-Webhook-Delivery-Id", delivery.Id.ToString());

            var response = await client.PostAsync(delivery.Webhook.Url, content, ct);

            delivery.StatusCode = (int)response.StatusCode;
            delivery.ResponseBody = await response.Content.ReadAsStringAsync(ct);
            delivery.IsSuccess = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.Webhook.SuccessCount++;
                delivery.Webhook.LastTriggeredAt = DateTime.UtcNow;
                _logger.LogInformation("Webhook delivered: {EventType} to {Url}", delivery.EventType, delivery.Webhook.Url);
            }
            else
            {
                delivery.Status = delivery.AttemptNumber >= (delivery.Webhook.RetryCount + 1)
                    ? WebhookDeliveryStatus.Failed
                    : WebhookDeliveryStatus.Pending;

                if (delivery.Status == WebhookDeliveryStatus.Failed)
                {
                    delivery.Webhook.FailureCount++;
                    delivery.Webhook.Status = WebhookStatus.Paused;
                }
            }
        }
        catch (Exception ex)
        {
            delivery.ResponseBody = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            delivery.IsSuccess = false;
            delivery.StatusCode = 0;

            delivery.Status = delivery.AttemptNumber >= (delivery.Webhook.RetryCount + 1)
                ? WebhookDeliveryStatus.Failed
                : WebhookDeliveryStatus.Pending;

            if (delivery.Status == WebhookDeliveryStatus.Failed)
            {
                delivery.Webhook.FailureCount++;
                delivery.Webhook.Status = WebhookStatus.Paused;
            }

            _logger.LogWarning(ex, "Webhook delivery attempt {Attempt} failed for {Url}", delivery.AttemptNumber, delivery.Webhook?.Url);
        }

        await context.SaveChangesAsync(ct);
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLower();
    }
}
