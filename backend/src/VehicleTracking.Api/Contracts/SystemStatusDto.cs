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
    int TrackedVehicles,
    int ActiveAlerts,
    DateTimeOffset? AlertsLastSuccessfulReadAt,
    DateTimeOffset? TripUpdatesLastSuccessfulReadAt,
    int TrackedTrips,
    long WebSocketBytesLastMinute,
    LiveFreshnessDto LiveFreshness)
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
        s.TrackedVehicles,
        s.ActiveAlerts,
        s.AlertsLastSuccessfulReadAt,
        s.TripUpdatesLastSuccessfulReadAt,
        s.TrackedTrips,
        s.WebSocketBytesLastMinute,
        LiveFreshnessDto.From(s.LiveFreshness));
}

/// <summary>Remembered vehicles grouped by the age of their last report; <c>OffTheMap</c> were silent longer than <c>StaleAfterMinutes</c>.</summary>
public sealed record LiveFreshnessDto(int UnderOneMinute, int OneToFiveMinutes, int FiveMinutesToStale, int OffTheMap, double StaleAfterMinutes)
{
    public static LiveFreshnessDto From(VehicleTracking.Application.Live.LiveFreshness f) =>
        new(f.UnderOneMinute, f.OneToFiveMinutes, f.FiveMinutesToStale, f.OffTheMap, f.StaleAfter.TotalMinutes);
}
