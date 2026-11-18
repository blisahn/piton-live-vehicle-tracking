using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

/// <summary>The latest known position of every vehicle. Its size follows the fleet, not time.</summary>
public interface ILiveState
{
    /// <summary>
    /// Takes the positions of one feed read. Keeps those that are newer than what is known,
    /// drops vehicles not heard from since <paramref name="evictBefore"/>, and reports exactly what changed.
    /// </summary>
    /// <param name="retainAfter">Older positions are neither stored nor remembered (the retention window).</param>
    /// <param name="liveAfter">Vehicles whose newest position is older are not shown to clients.</param>
    LiveChange Apply(IReadOnlyCollection<VehiclePosition> candidates, DateTimeOffset retainAfter, DateTimeOffset liveAfter);

    LiveSnapshot GetSnapshot();
}
