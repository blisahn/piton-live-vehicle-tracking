using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Alerts;
using VehicleTracking.Application.TripUpdates;

namespace VehicleTracking.Application.Status;

/// <summary>Gathers what the system status screen shows from the parts that own each number.</summary>
public sealed class SystemStatusService(
    IIngestionMetrics metrics,
    IIngestStatsReader stats,
    IConnectionCounter connections,
    IPositionSpool spool,
    ILiveState liveState,
    AlertService alerts,
    TripUpdateService tripUpdates,
    TimeProvider time)
{
    public async Task<SystemStatus> GetAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var counters = metrics.Read();
        var rowsLastHour = await stats.RowsWrittenSinceAsync(now - TimeSpan.FromHours(1), cancellationToken);

        return new SystemStatus(
            now,
            counters.LastSuccessfulReadAt,
            counters.LastReadDuration,
            counters.LastEntityCount,
            counters.LastChangedCount,
            counters.LastRejectedCount,
            counters.SuccessfulReads,
            counters.FailedReads,
            rowsLastHour,
            DatabaseAvailable: rowsLastHour is not null,
            spool.PendingBatches,
            connections.ConnectedClients,
            liveState.GetSnapshot().Vehicles.Count,
            alerts.Query(null, null, includeInactive: false, now).Count,
            alerts.Status.LastSuccessfulReadAt,
            tripUpdates.Status.LastSuccessfulReadAt,
            tripUpdates.Status.TripCount);
    }
}

/// <param name="RowsWrittenLastHour">Null while the database is unreachable.</param>
/// <param name="PendingSpoolBatches">Batches read from the feed but not yet committed to the database.</param>
public sealed record SystemStatus(
    DateTimeOffset ServerTime,
    DateTimeOffset? LastSuccessfulReadAt,
    TimeSpan LastReadDuration,
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
    int TrackedTrips);
