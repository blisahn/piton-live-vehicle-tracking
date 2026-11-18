using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.LiveState;

/// <summary>
/// Latest position per vehicle, in memory. One writer (the polling worker) and many readers
/// (connecting clients, REST). The lock is held only for in-memory work, never across I/O.
///
/// Two windows: every vehicle within the retention window is remembered, so a position the feed
/// repeats is recognised and never stored twice; only vehicles that reported recently are live,
/// i.e. shown to clients.
/// </summary>
internal sealed class InMemoryLiveState : ILiveState
{
    private readonly Dictionary<string, VehiclePosition> _vehicles = new();
    private readonly HashSet<string> _live = new();
    private readonly object _gate = new();
    private long _sequence;

    public LiveChange Apply(IReadOnlyCollection<VehiclePosition> candidates, DateTimeOffset retainAfter, DateTimeOffset liveAfter)
    {
        lock (_gate)
        {
            var stored = new List<VehiclePosition>();
            var upserted = new List<VehiclePosition>();
            foreach (var candidate in candidates)
            {
                // Outside the retention window: neither stored nor remembered.
                if (candidate.RecordedAt < retainAfter) continue;

                // The feed repeats positions and sometimes serves older data than we already have.
                if (_vehicles.TryGetValue(candidate.VehicleKey, out var known) && !candidate.IsNewerThan(known))
                    continue;

                _vehicles[candidate.VehicleKey] = candidate;
                stored.Add(candidate);

                // Stored either way; shown only if it is recent enough to be live.
                if (candidate.RecordedAt >= liveAfter)
                {
                    _live.Add(candidate.VehicleKey);
                    upserted.Add(candidate);
                }
            }

            // Collect first, change after: a dictionary cannot be modified while it is enumerated.
            var forgotten = new List<string>();
            var removed = new List<string>();
            foreach (var (key, position) in _vehicles)
            {
                if (position.RecordedAt < retainAfter) forgotten.Add(key);
                if (position.RecordedAt < liveAfter && _live.Contains(key)) removed.Add(key);
            }
            foreach (var key in forgotten) _vehicles.Remove(key);
            foreach (var key in removed) _live.Remove(key);

            if (upserted.Count > 0 || removed.Count > 0) _sequence++;

            return new LiveChange(_sequence, upserted, removed, stored);
        }
    }

    public LiveSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new LiveSnapshot(_sequence, _live.Select(key => _vehicles[key]).ToList());
        }
    }
}
