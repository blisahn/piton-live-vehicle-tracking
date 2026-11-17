using VehicleTracking.Application.Status;

namespace VehicleTracking.Api.Contracts;

/// <summary>System status as clients see it; durations are plain milliseconds.</summary>
public sealed record SystemStatusDto(
    DateTimeOffset ServerTime,
    DateTimeOffset? LastSuccessfulReadAt,
    double LastReadDurationMs,
    int LastEntityCount,
    int LastChangedCount,
    int LastRejectedCount,
    long SuccessfulReads,
    long FailedReads,
    long? RowsWrittenLastHour,
    bool DatabaseAvailable,
    int PendingSpoolBatches,
    int ConnectedClients,
    int TrackedVehicles)
{
    public static SystemStatusDto From(SystemStatus s) => new(
        s.ServerTime,
        s.LastSuccessfulReadAt,
        s.LastReadDuration.TotalMilliseconds,
        s.LastEntityCount,
        s.LastChangedCount,
        s.LastRejectedCount,
        s.SuccessfulReads,
        s.FailedReads,
        s.RowsWrittenLastHour,
        s.DatabaseAvailable,
        s.PendingSpoolBatches,
        s.ConnectedClients,
        s.TrackedVehicles);
}
