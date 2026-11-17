using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Infrastructure.Metrics;

/// <summary>
/// Counters for the status screen. Lock-free: the polling worker writes, any thread reads,
/// and each value is a single long or int, so Interlocked/Volatile is enough.
/// </summary>
internal sealed class IngestionMetrics : IIngestionMetrics
{
    // UTC ticks of the last successful read; 0 means "never".
    private long _lastSuccessTicks;
    private long _lastDurationTicks;
    private int _lastEntities;
    private int _lastChanged;
    private int _lastRejected;
    private long _successfulReads;
    private long _failedReads;

    public void RecordSuccessfulRead(DateTimeOffset at, TimeSpan duration, int entities, int changed, int rejected)
    {
        Volatile.Write(ref _lastDurationTicks, duration.Ticks);
        Volatile.Write(ref _lastEntities, entities);
        Volatile.Write(ref _lastChanged, changed);
        Volatile.Write(ref _lastRejected, rejected);
        Interlocked.Increment(ref _successfulReads);
        Volatile.Write(ref _lastSuccessTicks, at.UtcTicks);
    }

    public void RecordFailedRead() => Interlocked.Increment(ref _failedReads);

    public IngestionCounters Read()
    {
        var ticks = Volatile.Read(ref _lastSuccessTicks);
        return new IngestionCounters(
            LastSuccessfulReadAt: ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero),
            LastReadDuration: TimeSpan.FromTicks(Volatile.Read(ref _lastDurationTicks)),
            LastEntityCount: Volatile.Read(ref _lastEntities),
            LastChangedCount: Volatile.Read(ref _lastChanged),
            LastRejectedCount: Volatile.Read(ref _lastRejected),
            SuccessfulReads: Interlocked.Read(ref _successfulReads),
            FailedReads: Interlocked.Read(ref _failedReads));
    }
}
