using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// Knows every connected client and hands each live change to all of them.
/// Publishing only writes into each client's outbox in memory; it never waits for a socket,
/// so a slow client cannot delay the others or the polling loop that calls it.
///
/// It also announces alert version changes. The version reader is a Lazy because AlertService
/// is built with all IAlertPublishers (this hub among them); asking for the service only when
/// the first client connects, long after startup, keeps the dependency graph free of a cycle.
/// </summary>
internal sealed class VehicleSocketHub(ILiveState liveState, Lazy<IAlertVersionReader> alertVersion)
    : ILivePublisher, IAlertPublisher, IConnectionCounter
{
    private readonly object _gate = new();
    private readonly HashSet<ClientOutbox> _outboxes = new();
    private int _connectedClients;

    public int ConnectedClients => Volatile.Read(ref _connectedClients);

    public ClientOutbox Register()
    {
        var outbox = new ClientOutbox();

        // Taking the snapshot and joining the list happen under the same lock Publish uses,
        // so no change can fall into the gap between the two. A change that was applied to
        // the live state but not yet published is already inside the snapshot; when it is
        // published a moment later the outbox recognises it by its sequence and skips it.
        lock (_gate)
        {
            outbox.QueueSnapshot(liveState.GetSnapshot());
            outbox.QueueAlertsVersion(alertVersion.Value.Version);
            _outboxes.Add(outbox);
            _connectedClients = _outboxes.Count;
        }
        return outbox;
    }

    public void Unregister(ClientOutbox outbox)
    {
        lock (_gate)
        {
            _outboxes.Remove(outbox);
            _connectedClients = _outboxes.Count;
        }
    }

    public void Publish(LiveChange change)
    {
        lock (_gate)
        {
            foreach (var outbox in _outboxes)
                outbox.QueueChange(change);
        }
    }

    public void PublishAlertsVersion(long version)
    {
        lock (_gate)
        {
            foreach (var outbox in _outboxes)
                outbox.QueueAlertsVersion(version);
        }
    }
}
