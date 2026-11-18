using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.IntegrationTests;

/// <summary>
/// Old positions leave by dropping whole TimescaleDB chunks, not by deleting rows.
/// With a 12-minute retention the chunk is one minute, so this runs in seconds.
/// </summary>
public sealed class RetentionTests(DatabaseFixture db) : DatabaseTest(db)
{
    private const string ChunksSql = """
        select count(*) from timescaledb_information.chunks where hypertable_name = 'vehicle_positions'
        """;

    [DockerFact]
    public async Task The_chunk_interval_follows_the_retention_period()
    {
        var interval = await Db.ScalarAsync<string>("""
            select time_interval::text from timescaledb_information.dimensions
            where hypertable_name = 'vehicle_positions'
            """);

        Assert.Equal("00:01:00", interval);
    }

    [DockerFact]
    public async Task A_sweep_drops_chunks_older_than_the_retention_period_and_keeps_recent_rows()
    {
        var now = Positions.Now();
        var writer = Db.Services.GetRequiredService<IPositionWriter>();
        await writer.WriteAsync(Positions.Track("ATB:2667", now.AddMinutes(-30), 5), default); // long expired
        await writer.WriteAsync(Positions.Track("ATB:2667", now.AddMinutes(-20), 5), default); // long expired
        await writer.WriteAsync(Positions.Track("ATB:2667", now, 5), default);                 // recent
        var chunksBefore = await Db.ScalarAsync<long>(ChunksSql);
        Assert.True(chunksBefore >= 3, $"each minute should be its own chunk, found {chunksBefore}");

        var worker = ActivatorUtilities.CreateInstance<RetentionWorker>(Db.Services);
        await worker.StartAsync(default); // sweeps once right away
        try
        {
            await WaitUntilAsync(async () => await Db.ScalarAsync<long>(ChunksSql) <= chunksBefore - 2);
        }
        finally
        {
            await worker.StopAsync(default);
        }

        Assert.Equal(0L, await Db.ScalarAsync<long>(
            "select count(*) from vehicle_positions where recorded_at < @cutoff",
            ("cutoff", (now - DatabaseFixture.Retention).UtcDateTime)));
        Assert.Equal(5L, await Db.ScalarAsync<long>(
            "select count(*) from vehicle_positions where recorded_at >= @cutoff",
            ("cutoff", (now - DatabaseFixture.Retention).UtcDateTime)));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The retention sweep did not drop the old chunks.");
            await Task.Delay(100);
        }
    }
}
