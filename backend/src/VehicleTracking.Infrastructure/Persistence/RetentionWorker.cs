using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Keeps only the configured window of positions by dropping TimescaleDB chunks instead of deleting rows.
/// Dropping a chunk removes a whole table file at once, its cost does not depend on the number of rows,
/// it leaves no dead rows for vacuum, and it does not block the inserts going into the newest chunk.
/// Precision: a chunk is dropped only when ALL of its rows are older than the cutoff, so up to one chunk
/// interval of extra data can exist; read queries filter by time so callers never see it.
/// </summary>
internal sealed class RetentionWorker(
    NpgsqlDataSource dataSource,
    DatabaseReadiness readiness,
    IOptions<RetentionOptions> retention,
    TimeProvider time,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StatsRetention = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await readiness.WaitAsync(stoppingToken);

            var period = retention.Value.Period;
            using var timer = new PeriodicTimer(RetentionSchedule.SweepInterval(period));

            // Sweep once immediately, then on every tick.
            do
            {
                await SweepAsync(period, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task SweepAsync(TimeSpan period, CancellationToken ct)
    {
        try
        {
            var now = time.GetUtcNow().UtcDateTime;
            var cutoff = now - period;

            // drop_chunks takes "any", so the parameter type must be explicit.
            await using (var drop = dataSource.CreateCommand(
                "select count(*) from drop_chunks('vehicle_positions', older_than => @cutoff)"))
            {
                drop.Parameters.Add(new NpgsqlParameter("cutoff", NpgsqlDbType.TimestampTz) { Value = cutoff });
                var dropped = Convert.ToInt32(await drop.ExecuteScalarAsync(ct));
                if (dropped > 0)
                {
                    logger.LogInformation("Dropped {Count} chunks older than {Cutoff:O}", dropped, cutoff);
                }
            }

            // This table is tiny (one row per minute), so a plain delete is fine here, unlike the positions.
            await using var prune = dataSource.CreateCommand("delete from ingest_stats where minute < @cutoff");
            prune.Parameters.Add(new NpgsqlParameter("cutoff", NpgsqlDbType.TimestampTz) { Value = now - StatsRetention });
            await prune.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database may be down; the next sweep retries.
            logger.LogWarning(ex, "Retention sweep failed");
        }
    }
}
