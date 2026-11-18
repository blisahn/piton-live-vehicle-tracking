using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.IntegrationTests;

/// <summary>
/// The spool replays a batch after a crash or a database outage, so writing the same batch again
/// must not create duplicates or inflate the "rows written in the last hour" count.
/// </summary>
public sealed class PositionWriterTests(DatabaseFixture db) : DatabaseTest(db)
{
    private IPositionWriter Writer => Db.Services.GetRequiredService<IPositionWriter>();

    [DockerFact]
    public async Task Writing_the_same_batch_twice_stores_each_position_once()
    {
        var batch = Positions.Track("ATB:2667", Positions.Now(), 3);

        Assert.Equal(3, await Writer.WriteAsync(batch, default));
        Assert.Equal(0, await Writer.WriteAsync(batch, default));

        Assert.Equal(3L, await Db.ScalarAsync<long>("select count(*) from vehicle_positions"));
        Assert.Equal(3L, await Db.ScalarAsync<long>("select coalesce(sum(rows_written), 0) from ingest_stats"));
    }

    [DockerFact]
    public async Task A_replayed_batch_that_overlaps_stored_rows_adds_only_the_new_ones()
    {
        var now = Positions.Now();
        await Writer.WriteAsync(Positions.Track("ATB:2667", now.AddSeconds(-1), 2), default);

        var overlapping = Positions.Track("ATB:2667", now, 3); // two already stored, one new
        Assert.Equal(1, await Writer.WriteAsync(overlapping, default));

        Assert.Equal(3L, await Db.ScalarAsync<long>("select count(*) from vehicle_positions"));
        Assert.Equal(3L, await Db.ScalarAsync<long>("select coalesce(sum(rows_written), 0) from ingest_stats"));
    }

    [DockerFact]
    public async Task The_same_vehicle_number_at_two_operators_is_two_vehicles()
    {
        var now = Positions.Now();

        var written = await Writer.WriteAsync([Positions.At("ATB:2667", now), Positions.At("KOL:2667", now)], default);

        Assert.Equal(2, written);
    }

    [DockerFact]
    public async Task Positions_round_trip_with_their_fields()
    {
        var position = Positions.At("ATB:2667", Positions.Now());
        await Writer.WriteAsync([position], default);

        using var scope = Db.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IPositionHistoryReader>()
            .ReadNewestFirstAsync("ATB:2667", position.RecordedAt.AddMinutes(-1), position.RecordedAt.AddMinutes(1), 10, default);

        Assert.Equal(position, Assert.Single(stored));
    }
}
