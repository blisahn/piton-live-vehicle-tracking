using VehicleTracking.Application.TripUpdates;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Reads the current GTFS-Realtime trip updates.</summary>
public interface ITripUpdateFeedSource
{
    /// <exception cref="FeedReadException">The feed was unreachable or returned something unreadable.</exception>
    Task<TripUpdateSnapshot> ReadAsync(CancellationToken cancellationToken);
}
