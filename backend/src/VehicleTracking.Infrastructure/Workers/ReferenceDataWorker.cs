using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleTracking.Application.Transit;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.Infrastructure.Workers;

/// <summary>Loads the stored names at startup, then keeps resolving the ids the feed mentions.</summary>
internal sealed class ReferenceDataWorker(
    ReferenceDataService referenceData,
    DatabaseReadiness readiness,
    ILogger<ReferenceDataWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await readiness.WaitAsync(stoppingToken);

            try
            {
                await referenceData.LoadAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Stored line and stop names could not be loaded; continuing without them");
            }

            var retryDelay = InitialRetryDelay;
            var resolvedInBacklog = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var resolved = await referenceData.ResolvePendingAsync(stoppingToken);
                    retryDelay = InitialRetryDelay;

                    if (resolved > 0)
                    {
                        // More may be waiting: ask again right away.
                        resolvedInBacklog += resolved;
                        continue;
                    }

                    if (resolvedInBacklog > 0)
                    {
                        logger.LogInformation("Resolved names for {Count} line and stop ids", resolvedInBacklog);
                        resolvedInBacklog = 0;
                    }
                    await Task.Delay(IdleDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Resolving line and stop names failed. Retrying in {Delay}", retryDelay);
                    await Task.Delay(retryDelay, stoppingToken);
                    retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
