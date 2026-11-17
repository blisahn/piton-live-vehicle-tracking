namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>A row of vehicle_positions. Used for read queries only; writes go through <see cref="NpgsqlPositionWriter"/>.</summary>
public sealed class PositionRow
{
    public string VehicleKey { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? Bearing { get; set; }
    public float? Speed { get; set; }
    public string? RouteId { get; set; }
    public string? TripId { get; set; }
    public string? StopId { get; set; }
    public short? StopStatus { get; set; }
    public short? Occupancy { get; set; }
}
