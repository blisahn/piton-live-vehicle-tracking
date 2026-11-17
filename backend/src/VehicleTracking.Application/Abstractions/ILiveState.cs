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
    LiveChange Apply(IReadOnlyCollection<VehiclePosition> candidates, DateTimeOffset evictBefore);

    LiveSnapshot GetSnapshot();
}
