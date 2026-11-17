namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>A row of ingest_stats: how many positions were written in one minute. Read-only in EF.</summary>
public sealed class IngestStatRow
{
    public DateTimeOffset Minute { get; set; }
    public long RowsWritten { get; set; }
}
