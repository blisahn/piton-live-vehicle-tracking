using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Application.Live;

/// <summary>
/// Fills the live state from the database when the application starts. Without it a restart
/// while the feed is unreachable would leave the map empty until the feed comes back, even
/// though the last known position of every vehicle is sitting in the database.
/// </summary>
public sealed class LiveStateWarmUp(
    ILatestPositionReader reader,
    ILiveState liveState,
    IEnumerable<ILivePublisher> publishers,
    IOptions<RetentionOptions> retention,
    TimeProvider time)
{
    /// <returns>How many vehicles were restored.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var oldestKept = time.GetUtcNow() - retention.Value.Period;
        var stored = await reader.ReadLatestPerVehicleAsync(oldestKept, cancellationToken);

        // The feed may already have been read while the query ran. Apply only keeps positions
        // newer than what is known, so restoring late can never overwrite fresher live data.
        var change = liveState.Apply(stored, evictBefore: oldestKept);

        // These positions came from the database, so they are published but not spooled again.
        if (!change.IsEmpty)
            foreach (var publisher in publishers)
                publisher.Publish(change);

        return change.Upserted.Count;
    }
}
