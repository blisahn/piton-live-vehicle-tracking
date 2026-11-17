using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Alerts;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Workers;

/// <summary>Reads the alerts feed once at startup and then on every tick of its (slow) poll interval.</summary>
internal sealed class AlertPollingWorker(
    IAlertFeedSource source,
    AlertService alerts,
    IOptions<AlertOptions> options,
    ILogger<AlertPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);

        try
        {
            await RunCycleAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    // Never throws except for shutdown: one bad cycle must not stop the polling for good.
    // The previous alerts stay in place while the feed is failing.
    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            var snapshot = await source.ReadAsync(stoppingToken);
            await alerts.ApplyAsync(snapshot, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedReadException ex)
        {
            alerts.RecordFailure(ex.Message);
            logger.LogWarning("Alerts feed read failed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            alerts.RecordFailure(ex.Message);
            logger.LogError(ex, "Unexpected error in the alerts polling cycle");
        }
    }
}
