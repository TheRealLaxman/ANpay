using ANpay.Api.Services;

namespace ANpay.Api.Workers;

public class ScheduledTransferWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTransferWorker> _logger;
    private static int _isRunning;

    public ScheduledTransferWorker(IServiceProvider serviceProvider, ILogger<ScheduledTransferWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledTransferWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var scheduledTransferService = scope.ServiceProvider.GetRequiredService<ScheduledTransferService>();
                        await scheduledTransferService.ExecuteDueTransfersAsync();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isRunning, 0);
                    }
                }
                else
                {
                    _logger.LogDebug("Skipping scheduled transfer execution - previous run still in progress");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled transfers");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("ScheduledTransferWorker stopped");
    }
}
