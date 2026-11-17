using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and configures the hypertable in the background, retrying until the database
/// is reachable, then signals <see cref="DatabaseReadiness"/>.
/// </summary>
internal sealed class DatabaseInitializer(
    IServiceScopeFactory scopes,
    NpgsqlDataSource dataSource,
    DatabaseReadiness readiness,
    IOptions<RetentionOptions> retention,
    ILogger<DatabaseInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                try
                {
                    await InitializeAsync(stoppingToken);
                    readiness.MarkReady();
                    logger.LogInformation("Database is ready");
                    await WarmUpLiveStateAsync(stoppingToken);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Database initialization failed, retrying in {Delay}", RetryDelay);
                }

                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task WarmUpLiveStateAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var restored = await scope.ServiceProvider.GetRequiredService<LiveStateWarmUp>().RunAsync(ct);
            logger.LogInformation("Restored the last known position of {Count} vehicles from the database", restored);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not worth retrying: the next successful feed read fills the live state anyway.
            logger.LogWarning(ex, "Could not restore the live state from the database");
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            await db.Database.MigrateAsync(ct);
        }

        // Applies to chunks created from now on.
        await using var cmd = dataSource.CreateCommand("select set_chunk_time_interval('vehicle_positions', @interval)");
        cmd.Parameters.AddWithValue("interval", RetentionSchedule.ChunkInterval(retention.Value.Period));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
