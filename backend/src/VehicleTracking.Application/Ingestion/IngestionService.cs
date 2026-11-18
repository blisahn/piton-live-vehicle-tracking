using System.Diagnostics;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Application.Ingestion;

/// <summary>One read of the feed, end to end: read, clean, update live state, persist, publish.</summary>
public sealed class IngestionService(
    IFeedSource feed,
    SnapshotNormalizer normalizer,
    ILiveState liveState,
    IPositionSpool spool,
    IEnumerable<ILivePublisher> publishers,
    IIngestionMetrics metrics,
    IOptions<RetentionOptions> retention,
    IOptions<LiveOptions> live,
    TimeProvider time)
{
    /// <exception cref="FeedReadException">The feed could not be read; nothing was changed.</exception>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        FeedSnapshot snapshot;
        try
        {
            snapshot = await feed.ReadAsync(cancellationToken);
        }
        catch (FeedReadException)
        {
            metrics.RecordFailedRead();
            throw;
        }

        var now = time.GetUtcNow();
        var normalized = normalizer.Normalize(snapshot, now);

        // The feed carries entities last updated hours ago. Positions inside the retention window are
        // stored; only vehicles that reported recently are live. Nothing is removed while the feed is
        // unreachable, because this only runs after a successful read.
        var change = liveState.Apply(
            normalized.Positions,
            retainAfter: now - retention.Value.Period,
            liveAfter: now - live.Value.StaleAfter);

        // On disk first, then to clients: what a client has seen can always be found in history later.
        if (change.StoredPositions.Count > 0)
            await spool.AppendAsync(change.StoredPositions, cancellationToken);

        if (!change.IsEmpty)
            foreach (var publisher in publishers)
                publisher.Publish(change);

        metrics.RecordSuccessfulRead(
            now, Stopwatch.GetElapsedTime(started), normalized.EntityCount, change.StoredPositions.Count, normalized.RejectedCount);
    }
}
