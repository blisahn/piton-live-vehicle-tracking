namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>A row of transit_lines. Written by <see cref="NpgsqlReferenceDataStore"/>.</summary>
public sealed class TransitLineRow
{
    public string Id { get; set; } = string.Empty;
    public string? PublicCode { get; set; }
    public string? Name { get; set; }
    public string? TransportMode { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}

/// <summary>A row of transit_stops. Written by <see cref="NpgsqlReferenceDataStore"/>.</summary>
public sealed class TransitStopRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}
