using Microsoft.EntityFrameworkCore;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Reads stored positions of one vehicle. The query is served entirely by the primary key index
/// (vehicle_key, recorded_at): one index range scan, and TimescaleDB only opens the chunks that overlap the time range.
/// </summary>
internal sealed class EfPositionHistoryReader(TrackingDbContext db) : IPositionHistoryReader
{
    public async Task<IReadOnlyList<VehiclePosition>> ReadNewestFirstAsync(
        string vehicleKey, DateTimeOffset from, DateTimeOffset before, int take, CancellationToken cancellationToken)
    {
        // Npgsql only accepts offset-zero DateTimeOffset values for timestamptz parameters.
        var fromUtc = from.ToUniversalTime();
        var beforeUtc = before.ToUniversalTime();

        var rows = await db.Positions
            .AsNoTracking()
            .Where(p => p.VehicleKey == vehicleKey && p.RecordedAt >= fromUtc && p.RecordedAt < beforeUtc)
            .OrderByDescending(p => p.RecordedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToDomain()).ToList();
    }
}
