namespace VehicleTracking.Application.Abstractions;

public interface IIngestStatsReader
{
    /// <summary>Rows written to the database since <paramref name="since"/>, or null when the database cannot be asked right now.</summary>
    Task<long?> RowsWrittenSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
