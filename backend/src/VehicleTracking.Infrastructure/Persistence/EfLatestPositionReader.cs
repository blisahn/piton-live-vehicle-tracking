using Microsoft.EntityFrameworkCore;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// One row per vehicle: its newest. DISTINCT ON walks the primary key index (vehicle_key, recorded_at)
/// and keeps the first row of each vehicle, which with this ordering is the newest one.
/// LINQ cannot express DISTINCT ON, so this one query is written as SQL.
/// </summary>
internal sealed class EfLatestPositionReader(TrackingDbContext db) : ILatestPositionReader
{
    public async Task<IReadOnlyList<VehiclePosition>> ReadLatestPerVehicleAsync(
        DateTimeOffset since, CancellationToken cancellationToken)
    {
        // Npgsql only accepts offset-zero DateTimeOffset values for timestamptz parameters.
        var sinceUtc = since.ToUniversalTime();

        var rows = await db.Positions
            .FromSql($"""
                select distinct on (vehicle_key) *
                from vehicle_positions
                where recorded_at >= {sinceUtc}
                order by vehicle_key, recorded_at desc
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToDomain()).ToList();
    }
}
