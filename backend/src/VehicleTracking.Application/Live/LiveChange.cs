using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>
/// What one feed read changed. <see cref="Upserted"/> and <see cref="RemovedVehicleKeys"/> are what
/// clients see (live vehicles only); <see cref="Stored"/> is every new position to persist, which
/// also includes positions too old to show but still inside the retention window.
/// <see cref="Sequence"/> only grows when what clients see changed.
/// </summary>
public sealed record LiveChange(
    long Sequence,
    IReadOnlyList<VehiclePosition> Upserted,
    IReadOnlyList<string> RemovedVehicleKeys,
    IReadOnlyList<VehiclePosition>? Stored = null)
{
    public bool IsEmpty => Upserted.Count == 0 && RemovedVehicleKeys.Count == 0;

    /// <summary>New positions to persist; defaults to <see cref="Upserted"/> when not given.</summary>
    public IReadOnlyList<VehiclePosition> StoredPositions => Stored ?? Upserted;
}

/// <summary>Every vehicle as of <see cref="Sequence"/>.</summary>
public sealed record LiveSnapshot(long Sequence, IReadOnlyList<VehiclePosition> Vehicles);
