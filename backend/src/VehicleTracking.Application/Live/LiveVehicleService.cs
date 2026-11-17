using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Application.Live;

/// <summary>Answers "where is every vehicle right now", optionally for a single route.</summary>
public sealed class LiveVehicleService(ILiveState liveState)
{
    public LiveSnapshot GetCurrent(string? routeId)
    {
        var snapshot = liveState.GetSnapshot();
        if (string.IsNullOrEmpty(routeId)) return snapshot;

        // Route ids are identifiers such as "RUT:Line:31", so they are compared exactly.
        var onRoute = snapshot.Vehicles
            .Where(vehicle => string.Equals(vehicle.RouteId, routeId, StringComparison.Ordinal))
            .ToList();
        return new LiveSnapshot(snapshot.Sequence, onRoute);
    }
}
