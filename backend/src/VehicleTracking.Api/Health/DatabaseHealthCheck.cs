using Microsoft.Extensions.Diagnostics.HealthChecks;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Api.Health;

/// <summary>Healthy when the database answers a small query right now.</summary>
internal sealed class DatabaseHealthCheck(IIngestStatsReader stats, IPositionSpool spool, TimeProvider time) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var rows = await stats.RowsWrittenSinceAsync(time.GetUtcNow() - TimeSpan.FromMinutes(1), cancellationToken);
        var data = new Dictionary<string, object> { ["pendingSpoolBatches"] = spool.PendingBatches };

        // Collection keeps running without the database (positions wait in the spool), so this is
        // "degraded", not "unhealthy": nothing is being lost, but history and statistics are unavailable.
        return rows is null
            ? HealthCheckResult.Degraded("The database cannot be reached; positions are waiting in the spool.", data: data)
            : HealthCheckResult.Healthy("The database is reachable.", data);
    }
}
