using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Infrastructure.Workers;

/// <summary>Drains the spool into the database, oldest batch first, retrying a failed batch until it succeeds.</summary>
internal sealed class PositionWriterWorker(
    IPositionSpool spool,
    IPositionWriter writer,
    ILogger<PositionWriterWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = InitialRetryDelay;
        var recovering = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var batch = await spool.PeekAsync(stoppingToken);
                    if (batch is null)
                    {
                        if (recovering)
                        {
                            logger.LogInformation("The spool backlog has been fully written to the database");
                            recovering = false;
                        }
                        await Task.Delay(IdleDelay, stoppingToken);
                        continue;
                    }

                    await writer.WriteAsync(batch.Positions, stoppingToken);

                    // Completing only after the commit is safe: if the process dies between the commit
                    // and the delete, the batch is written again and the database skips the duplicates.
                    await spool.CompleteAsync(batch, stoppingToken);
                    retryDelay = InitialRetryDelay;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var pending = spool.PendingBatches;
                    if (pending > 1) recovering = true;

                    logger.LogWarning(ex, "Writing a batch failed; {Pending} batches are waiting in the spool. Retrying in {Delay}",
                        pending, retryDelay);

                    // The batch was not completed, so the next loop picks up the same one.
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
