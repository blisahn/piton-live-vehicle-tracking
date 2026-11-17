using Microsoft.Extensions.Diagnostics.HealthChecks;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Api.Health;

/// <summary>Healthy while the feed has been read successfully in the recent past.</summary>
internal sealed class FeedHealthCheck(IIngestionMetrics metrics, TimeProvider time) : IHealthCheck
{
    /// <summary>Ten missed polls in a row at the default interval.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var counters = metrics.Read();
        if (counters.LastSuccessfulReadAt is not { } lastRead)
            return Task.FromResult(HealthCheckResult.Unhealthy("The feed has not been read successfully yet."));

        var age = time.GetUtcNow() - lastRead;
        var data = new Dictionary<string, object>
        {
            ["lastSuccessfulReadAt"] = lastRead,
            ["failedReads"] = counters.FailedReads,
        };

        return Task.FromResult(age <= MaxAge
            ? HealthCheckResult.Healthy("The feed is being read.", data)
            : HealthCheckResult.Unhealthy($"The last successful read was {age.TotalSeconds:F0} seconds ago.", data: data));
    }
}
