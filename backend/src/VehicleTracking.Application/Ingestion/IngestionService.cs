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

        // Positions older than the retention window are neither shown nor stored:
        // the feed carries entities that were last updated many hours ago.
        var change = liveState.Apply(normalized.Positions, evictBefore: now - retention.Value.Period);

        // On disk first, then to clients: what a client has seen can always be found in history later.
        if (change.Upserted.Count > 0)
            await spool.AppendAsync(change.Upserted, cancellationToken);

        if (!change.IsEmpty)
            foreach (var publisher in publishers)
                publisher.Publish(change);

        metrics.RecordSuccessfulRead(
            now, Stopwatch.GetElapsedTime(started), normalized.EntityCount, change.Upserted.Count, normalized.RejectedCount);
    }
}
