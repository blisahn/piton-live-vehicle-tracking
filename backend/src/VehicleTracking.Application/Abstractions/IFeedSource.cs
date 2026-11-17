using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Reads the current state of the vehicle positions feed.</summary>
public interface IFeedSource
{
    /// <exception cref="FeedReadException">The feed was unreachable or returned something unreadable.</exception>
    Task<FeedSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class FeedReadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>One response of the feed, translated field by field with no judgement applied.</summary>
public sealed record FeedSnapshot(DateTimeOffset? GeneratedAt, IReadOnlyList<FeedVehicle> Vehicles);

/// <summary>One feed entity as published. Anything the source left out is null.</summary>
public sealed record FeedVehicle(
    string EntityId,
    string? VehicleId,
    DateTimeOffset? Timestamp,
    double? Latitude,
    double? Longitude,
    float? Bearing,
    float? Speed,
    string? RouteId,
    string? TripId,
    string? StopId,
    VehicleStopStatus? StopStatus,
    OccupancyStatus? Occupancy);
