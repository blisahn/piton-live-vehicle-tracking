using Npgsql;
using NpgsqlTypes;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Writes a batch of positions with plain Npgsql. Two decisions:
/// (a) One statement with unnest sends the whole batch in a single round trip. EF Core's SaveChanges would
/// track every row and cannot express ON CONFLICT, and COPY cannot skip duplicates without a temporary table;
/// at about 1-2k rows per batch unnest is the simplest thing that is fast enough.
/// (b) ON CONFLICT DO NOTHING makes the write idempotent, which is what lets the spool replay a batch
/// after a crash or a database outage without creating duplicates.
/// </summary>
internal sealed class NpgsqlPositionWriter(NpgsqlDataSource dataSource, DatabaseReadiness readiness) : IPositionWriter
{
    private const string InsertPositionsSql = """
        insert into vehicle_positions
            (vehicle_key, recorded_at, latitude, longitude, bearing, speed, route_id, trip_id, stop_id, stop_status, occupancy)
        select * from unnest(@keys, @times, @lats, @lons, @bearings, @speeds, @routes, @trips, @stops, @statuses, @occupancies)
        on conflict (vehicle_key, recorded_at) do nothing
        """;

    private const string UpsertStatsSql = """
        insert into ingest_stats (minute, rows_written) values (date_trunc('minute', now()), @rows)
        on conflict (minute) do update set rows_written = ingest_stats.rows_written + excluded.rows_written
        """;

    public async Task<int> WriteAsync(IReadOnlyCollection<VehiclePosition> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return 0;

        await readiness.WaitAsync(cancellationToken);

        var keys = new string[batch.Count];
        var times = new DateTime[batch.Count];
        var lats = new double[batch.Count];
        var lons = new double[batch.Count];
        var bearings = new float?[batch.Count];
        var speeds = new float?[batch.Count];
        var routes = new string?[batch.Count];
        var trips = new string?[batch.Count];
        var stops = new string?[batch.Count];
        var statuses = new short?[batch.Count];
        var occupancies = new short?[batch.Count];

        var i = 0;
        foreach (var p in batch)
        {
            keys[i] = p.VehicleKey;
            times[i] = p.RecordedAt.UtcDateTime; // Npgsql requires a UTC DateTime for timestamptz
            lats[i] = p.Latitude;
            lons[i] = p.Longitude;
            bearings[i] = p.Bearing;
            speeds[i] = p.Speed;
            routes[i] = p.RouteId;
            trips[i] = p.TripId;
            stops[i] = p.StopId;
            statuses[i] = (short?)p.StopStatus;
            occupancies[i] = (short?)p.Occupancy;
            i++;
        }

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        int inserted;
        await using (var cmd = new NpgsqlCommand(InsertPositionsSql, conn, tx))
        {
            // Explicit types so PostgreSQL knows the element type even when every value is null.
            cmd.Parameters.Add(new NpgsqlParameter("keys", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys });
            cmd.Parameters.Add(new NpgsqlParameter("times", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = times });
            cmd.Parameters.Add(new NpgsqlParameter("lats", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = lats });
            cmd.Parameters.Add(new NpgsqlParameter("lons", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = lons });
            cmd.Parameters.Add(new NpgsqlParameter("bearings", NpgsqlDbType.Array | NpgsqlDbType.Real) { Value = bearings });
            cmd.Parameters.Add(new NpgsqlParameter("speeds", NpgsqlDbType.Array | NpgsqlDbType.Real) { Value = speeds });
            cmd.Parameters.Add(new NpgsqlParameter("routes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = routes });
            cmd.Parameters.Add(new NpgsqlParameter("trips", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = trips });
            cmd.Parameters.Add(new NpgsqlParameter("stops", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = stops });
            cmd.Parameters.Add(new NpgsqlParameter("statuses", NpgsqlDbType.Array | NpgsqlDbType.Smallint) { Value = statuses });
            cmd.Parameters.Add(new NpgsqlParameter("occupancies", NpgsqlDbType.Array | NpgsqlDbType.Smallint) { Value = occupancies });
            inserted = await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (inserted > 0)
        {
            await using var stats = new NpgsqlCommand(UpsertStatsSql, conn, tx);
            stats.Parameters.Add(new NpgsqlParameter("rows", NpgsqlDbType.Bigint) { Value = (long)inserted });
            await stats.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return inserted;
    }
}
