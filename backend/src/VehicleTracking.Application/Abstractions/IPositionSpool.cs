using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

/// <summary>
/// Durable queue between reading the feed and writing the database. A batch stays here until
/// the database has committed it, so a database outage or a restart does not lose positions.
/// </summary>
public interface IPositionSpool
{
    /// <summary>Returns only after the batch is safely on disk.</summary>
    Task AppendAsync(IReadOnlyCollection<VehiclePosition> batch, CancellationToken cancellationToken);

    /// <summary>The oldest batch not yet completed, or null when the spool is empty.</summary>
    Task<SpooledBatch?> PeekAsync(CancellationToken cancellationToken);

    /// <summary>Removes a batch. Call only after the database committed it.</summary>
    Task CompleteAsync(SpooledBatch batch, CancellationToken cancellationToken);

    int PendingBatches { get; }
}

public sealed record SpooledBatch(string Id, IReadOnlyList<VehiclePosition> Positions);
