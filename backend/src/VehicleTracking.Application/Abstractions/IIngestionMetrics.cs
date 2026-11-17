namespace VehicleTracking.Application.Abstractions;

/// <summary>Counters behind the system status screen. Safe to call from any thread.</summary>
public interface IIngestionMetrics
{
    void RecordSuccessfulRead(DateTimeOffset at, TimeSpan duration, int entities, int changed, int rejected);

    void RecordFailedRead();

    IngestionCounters Read();
}

/// <param name="LastSuccessfulReadAt">Null until the first read succeeds.</param>
/// <param name="FailedReads">Failed reads since the process started.</param>
public sealed record IngestionCounters(
    DateTimeOffset? LastSuccessfulReadAt,
    TimeSpan LastReadDuration,
    int LastEntityCount,
    int LastChangedCount,
    int LastRejectedCount,
    long SuccessfulReads,
    long FailedReads);
