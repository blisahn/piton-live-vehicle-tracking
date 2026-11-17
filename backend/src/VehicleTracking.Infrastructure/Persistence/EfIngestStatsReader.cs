using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Sums the per-minute write counters. The status endpoint must answer even when the database is down,
/// so null means "unknown right now" instead of an error.
/// </summary>
internal sealed class EfIngestStatsReader(
    TrackingDbContext db,
    DatabaseReadiness readiness,
    ILogger<EfIngestStatsReader> logger) : IIngestStatsReader
{
    public async Task<long?> RowsWrittenSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        if (!readiness.IsReady) return null;

        var sinceUtc = since.ToUniversalTime();
        try
        {
            return await db.IngestStats
                .AsNoTracking()
                .Where(s => s.Minute >= sinceUtc)
                .SumAsync(s => (long?)s.RowsWritten, cancellationToken) ?? 0;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogDebug(ex, "Database unreachable while reading ingest stats");
            return null;
        }
    }
}
