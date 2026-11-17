using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Contracts;

/// <summary>A vehicle position as clients see it, over both REST and WebSocket.</summary>
/// <param name="Status">GTFS-Realtime VehicleStopStatus: 0 incoming at, 1 stopped at, 2 in transit to.</param>
/// <param name="Occupancy">GTFS-Realtime OccupancyStatus, 0 (empty) to 8 (not boardable).</param>
public sealed record VehicleDto(
    string Key,
    DateTimeOffset RecordedAt,
    double Lat,
    double Lon,
    float? Bearing,
    float? Speed,
    string? RouteId,
    string? TripId,
    string? StopId,
    int? Status,
    int? Occupancy)
{
    public static VehicleDto From(VehiclePosition p) => new(
        p.VehicleKey, p.RecordedAt, p.Latitude, p.Longitude, p.Bearing, p.Speed,
        p.RouteId, p.TripId, p.StopId, (int?)p.StopStatus, (int?)p.Occupancy);
}
