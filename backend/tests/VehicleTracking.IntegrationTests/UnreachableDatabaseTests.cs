using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.IntegrationTests;

/// <summary>
/// The status endpoint and the health check must keep answering while the database is down.
/// No container needed: the failure is produced for real by a host name that never resolves.
/// </summary>
public sealed class UnreachableDatabaseTests
{
    [Fact]
    public async Task Ingest_stats_are_unknown_when_the_database_host_name_does_not_resolve()
    {
        // `docker compose stop db` removes the "db" host name, so Npgsql fails with a bare SocketException
        // from the DNS lookup. ".invalid" is reserved and never resolves (RFC 6761), which reproduces that.
        var options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql("Host=db.invalid;Port=5432;Database=x;Username=x;Password=x;Timeout=5")
            .Options;
        await using var db = new TrackingDbContext(options);
        var readiness = new DatabaseReadiness();
        readiness.MarkReady();
        var reader = new EfIngestStatsReader(db, readiness, NullLogger<EfIngestStatsReader>.Instance);

        var rows = await reader.RowsWrittenSinceAsync(DateTimeOffset.UtcNow.AddHours(-1), default);

        Assert.Null(rows);
    }
}
