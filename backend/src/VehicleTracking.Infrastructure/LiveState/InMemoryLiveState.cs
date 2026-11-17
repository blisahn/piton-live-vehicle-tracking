using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.LiveState;

/// <summary>
/// Latest position per vehicle, in memory. One writer (the polling worker) and many readers
/// (connecting clients, REST). The lock is held only for in-memory work, never across I/O.
/// </summary>
internal sealed class InMemoryLiveState : ILiveState
{
    private readonly Dictionary<string, VehiclePosition> _vehicles = new();
    private readonly object _gate = new();
    private long _sequence;

    public LiveChange Apply(IReadOnlyCollection<VehiclePosition> candidates, DateTimeOffset evictBefore)
    {
        lock (_gate)
        {
            var upserted = new List<VehiclePosition>();
            foreach (var candidate in candidates)
            {
                // Too old to show or store.
                if (candidate.RecordedAt < evictBefore) continue;

                // The feed repeats positions and sometimes serves older data than we already have.
                if (_vehicles.TryGetValue(candidate.VehicleKey, out var known) && !candidate.IsNewerThan(known))
                    continue;

                _vehicles[candidate.VehicleKey] = candidate;
                upserted.Add(candidate);
            }

            // Collect first, remove after: a dictionary cannot be modified while it is enumerated.
            var removed = new List<string>();
            foreach (var (key, position) in _vehicles)
            {
                if (position.RecordedAt < evictBefore) removed.Add(key);
            }
            foreach (var key in removed) _vehicles.Remove(key);

            if (upserted.Count > 0 || removed.Count > 0) _sequence++;

            return new LiveChange(_sequence, upserted, removed);
        }
    }

    public LiveSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new LiveSnapshot(_sequence, _vehicles.Values.ToList());
        }
    }
}
