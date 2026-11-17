using VehicleTracking.Application.Transit;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Keeps the names already looked up, so a restart does not have to ask for them again.</summary>
public interface IReferenceDataStore
{
    Task<StoredReferenceData> LoadAllAsync(CancellationToken cancellationToken);

    /// <summary>Inserts new entries and overwrites existing ones, stamping them with <paramref name="fetchedAt"/>.</summary>
    Task SaveAsync(
        IReadOnlyCollection<TransitLine> lines,
        IReadOnlyCollection<TransitStop> stops,
        DateTimeOffset fetchedAt,
        CancellationToken cancellationToken);
}

/// <param name="StaleLineIds">Lines last fetched long enough ago to be worth asking for again.</param>
/// <param name="StaleStopIds">Stops last fetched long enough ago to be worth asking for again.</param>
public sealed record StoredReferenceData(
    IReadOnlyList<TransitLine> Lines,
    IReadOnlyList<TransitStop> Stops,
    IReadOnlyList<string> StaleLineIds,
    IReadOnlyList<string> StaleStopIds);
