using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Infrastructure.Workers;

/// <summary>Reads the feed once at startup and then on every tick of the poll interval.</summary>
internal sealed class FeedPollingWorker(
    IngestionService ingestion,
    IOptions<FeedOptions> options,
    ILogger<FeedPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer does not pile up ticks: if a cycle runs long, at most one tick waits,
        // so slow cycles never turn into a burst of requests.
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
    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ingestion.RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedReadException ex)
        {
            // Expected now and then; the service has already counted it.
            logger.LogWarning("Feed read failed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in the polling cycle");
        }
    }
}
