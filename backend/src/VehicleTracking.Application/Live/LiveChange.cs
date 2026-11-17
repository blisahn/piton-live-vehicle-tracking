using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>What one feed read changed. <see cref="Sequence"/> only grows when something changed.</summary>
public sealed record LiveChange(
    long Sequence,
    IReadOnlyList<VehiclePosition> Upserted,
    IReadOnlyList<string> RemovedVehicleKeys)
{
    public bool IsEmpty => Upserted.Count == 0 && RemovedVehicleKeys.Count == 0;
}

/// <summary>Every vehicle as of <see cref="Sequence"/>.</summary>
public sealed record LiveSnapshot(long Sequence, IReadOnlyList<VehiclePosition> Vehicles);
