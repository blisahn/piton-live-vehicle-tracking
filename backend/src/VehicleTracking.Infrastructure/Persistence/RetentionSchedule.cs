namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>Derives chunk size and sweep frequency from the configured retention period.</summary>
internal static class RetentionSchedule
{
    private static readonly TimeSpan MinChunkInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxSweepInterval = TimeSpan.FromMinutes(5);

    // Retention is enforced by dropping whole chunks, so chunk size decides how precisely
    // the window is kept: at most one chunk interval of extra data.
    public static TimeSpan ChunkInterval(TimeSpan retention)
    {
        var interval = retention / 12;
        return interval < MinChunkInterval ? MinChunkInterval : interval;
    }

    // Sweeping twice per chunk interval means a chunk is dropped at most half an interval after
    // it expires. The chunk floor of one minute already keeps this at 30 seconds or more.
    public static TimeSpan SweepInterval(TimeSpan retention)
    {
        var interval = ChunkInterval(retention) / 2;
        return interval > MaxSweepInterval ? MaxSweepInterval : interval;
    }
}
