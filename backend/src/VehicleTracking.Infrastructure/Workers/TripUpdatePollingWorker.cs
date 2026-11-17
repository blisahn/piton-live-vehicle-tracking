using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.TripUpdates;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Workers;

/// <summary>Reads the trip updates feed once at startup and then on every tick of its (lazy) poll interval.</summary>
internal sealed class TripUpdatePollingWorker(
    ITripUpdateFeedSource source,
    TripUpdateService updates,
    IOptions<TripUpdateOptions> options,
    ILogger<TripUpdatePollingWorker> logger) : BackgroundService
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
    // The previous trip updates stay in place while the feed is failing.
    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            var snapshot = await source.ReadAsync(stoppingToken);
            updates.Apply(snapshot);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedReadException ex)
        {
            updates.RecordFailure(ex.Message);
            logger.LogWarning("Trip updates feed read failed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            updates.RecordFailure(ex.Message);
            logger.LogError(ex, "Unexpected error in the trip updates polling cycle");
        }
    }
}
