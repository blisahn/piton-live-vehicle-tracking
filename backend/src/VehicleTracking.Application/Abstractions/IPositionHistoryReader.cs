using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

public interface IPositionHistoryReader
{
    /// <summary>
    /// Positions of one vehicle with <paramref name="from"/> ≤ RecordedAt &lt; <paramref name="before"/>,
    /// newest first, at most <paramref name="take"/> of them.
    /// </summary>
    Task<IReadOnlyList<VehiclePosition>> ReadNewestFirstAsync(
        string vehicleKey, DateTimeOffset from, DateTimeOffset before, int take, CancellationToken cancellationToken);
}
