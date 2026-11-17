namespace VehicleTracking.Domain.Vehicles;

/// <summary>One measured position of one vehicle. Immutable.</summary>
/// <param name="VehicleKey">See <see cref="Vehicles.VehicleKey"/>.</param>
/// <param name="RecordedAt">When the vehicle measured the position, not when we read it.</param>
public sealed record VehiclePosition(
    string VehicleKey,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude,
    float? Bearing,
    float? Speed,
    string? RouteId,
    string? TripId,
    string? StopId,
    VehicleStopStatus? StopStatus,
    OccupancyStatus? Occupancy)
{
    public bool IsNewerThan(VehiclePosition other) => RecordedAt > other.RecordedAt;
}
