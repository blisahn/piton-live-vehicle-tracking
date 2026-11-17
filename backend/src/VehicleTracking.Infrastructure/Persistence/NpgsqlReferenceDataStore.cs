using Npgsql;
using NpgsqlTypes;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Transit;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Stores looked-up line and stop names. A singleton on purpose: ReferenceDataService is a singleton,
/// so this uses the shared NpgsqlDataSource instead of the scoped DbContext. Uses the same
/// unnest upsert as <see cref="NpgsqlPositionWriter"/>.
/// </summary>
internal sealed class NpgsqlReferenceDataStore(NpgsqlDataSource dataSource, DatabaseReadiness readiness, TimeProvider time) : IReferenceDataStore
{
    /// <summary>Names rarely change; a day is fresh enough.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private const string UpsertLinesSql = """
        insert into transit_lines (id, public_code, name, transport_mode, fetched_at)
        select * from unnest(@ids, @codes, @names, @modes, @fetched)
        on conflict (id) do update set
            public_code = excluded.public_code, name = excluded.name,
            transport_mode = excluded.transport_mode, fetched_at = excluded.fetched_at
        """;

    private const string UpsertStopsSql = """
        insert into transit_stops (id, name, latitude, longitude, fetched_at)
        select * from unnest(@ids, @names, @lats, @lons, @fetched)
        on conflict (id) do update set
            name = excluded.name, latitude = excluded.latitude,
            longitude = excluded.longitude, fetched_at = excluded.fetched_at
        """;

    public async Task<StoredReferenceData> LoadAllAsync(CancellationToken cancellationToken)
    {
        await readiness.WaitAsync(cancellationToken);

        var staleBefore = time.GetUtcNow() - MaxAge;
        var lines = new List<TransitLine>();
        var stops = new List<TransitStop>();
        var staleLineIds = new List<string>();
        var staleStopIds = new List<string>();

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var cmd = new NpgsqlCommand("select id, public_code, name, transport_mode, fetched_at from transit_lines", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                lines.Add(new TransitLine(
                    id,
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
                if (reader.GetFieldValue<DateTimeOffset>(4) < staleBefore) staleLineIds.Add(id);
            }
        }

        await using (var cmd = new NpgsqlCommand("select id, name, latitude, longitude, fetched_at from transit_stops", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                stops.Add(new TransitStop(id, reader.GetString(1), reader.GetDouble(2), reader.GetDouble(3)));
                if (reader.GetFieldValue<DateTimeOffset>(4) < staleBefore) staleStopIds.Add(id);
            }
        }

        return new StoredReferenceData(lines, stops, staleLineIds, staleStopIds);
    }

    public async Task SaveAsync(
        IReadOnlyCollection<TransitLine> lines,
        IReadOnlyCollection<TransitStop> stops,
        DateTimeOffset fetchedAt,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0 && stops.Count == 0) return;

        await readiness.WaitAsync(cancellationToken);

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (lines.Count > 0)
        {
            await using var cmd = new NpgsqlCommand(UpsertLinesSql, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lines.Select(l => l.Id).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("codes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lines.Select(l => l.PublicCode).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("names", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lines.Select(l => l.Name).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("modes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lines.Select(l => l.TransportMode).ToArray() });
            cmd.Parameters.Add(FetchedParameter(lines.Count, fetchedAt));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (stops.Count > 0)
        {
            await using var cmd = new NpgsqlCommand(UpsertStopsSql, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = stops.Select(s => s.Id).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("names", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = stops.Select(s => s.Name).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("lats", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = stops.Select(s => s.Latitude).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("lons", NpgsqlDbType.Array | NpgsqlDbType.Double) { Value = stops.Select(s => s.Longitude).ToArray() });
            cmd.Parameters.Add(FetchedParameter(stops.Count, fetchedAt));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static NpgsqlParameter FetchedParameter(int count, DateTimeOffset fetchedAt) =>
        // Npgsql requires a UTC DateTime for timestamptz.
        new("fetched", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = Enumerable.Repeat(fetchedAt.UtcDateTime, count).ToArray() };
}
