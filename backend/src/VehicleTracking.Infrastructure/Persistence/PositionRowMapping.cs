using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

internal static class PositionRowMapping
{
    /// <summary>Runs in memory after a query has materialised, so the enum casts never have to be translated to SQL.</summary>
    public static VehiclePosition ToDomain(this PositionRow row) => new(
        row.VehicleKey,
        row.RecordedAt,
        row.Latitude,
        row.Longitude,
        row.Bearing,
        row.Speed,
        row.RouteId,
        row.TripId,
        row.StopId,
        (VehicleStopStatus?)row.StopStatus,
        (OccupancyStatus?)row.Occupancy);
}
