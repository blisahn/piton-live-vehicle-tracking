using VehicleTracking.Application.Alerts;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Reads the current service alerts.</summary>
public interface IAlertFeedSource
{
    /// <exception cref="FeedReadException">The feed was unreachable or returned something unreadable.</exception>
    Task<AlertFeedSnapshot> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>One response of the alerts feed. Texts carry the language tag as published (or null) and are not yet normalized.</summary>
public sealed record AlertFeedSnapshot(DateTimeOffset? GeneratedAt, IReadOnlyList<ServiceAlert> Alerts);
