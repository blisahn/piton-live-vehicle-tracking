using System.Threading.Channels;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// What one client still has to be sent. It holds state, not a queue of messages: changes that
/// arrive while the client is busy are merged into "the latest position per vehicle". A slow
/// client therefore costs at most one entry per vehicle in memory and, when it catches up,
/// receives one merged delta instead of everything it fell behind on.
///
/// Two threads touch it: the polling thread adds changes, the client's send loop takes them.
/// The lock only ever covers in-memory work.
/// </summary>
internal sealed class ClientOutbox
{
    private readonly object _sync = new();

    // A one-slot "there is work" flag. Extra signals while it is already set are dropped.
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private LiveSnapshot? _pendingSnapshot;
    private long _baseSequence;
    private readonly Dictionary<string, VehiclePosition> _upserts = new();
    private readonly HashSet<string> _removals = new();
    private long _fromSequence;   // 0 means no delta is pending
    private long _toSequence;

    // Latest wins, like everything here: a client only needs to know the newest version to refetch.
    // A snapshot does not clear it, because alerts are independent of vehicle sequences.
    private long? _pendingAlertsVersion;

    public void QueueSnapshot(LiveSnapshot snapshot)
    {
        lock (_sync)
        {
            // A snapshot replaces everything the client knows, so older pending changes are irrelevant.
            _pendingSnapshot = snapshot;
            _baseSequence = snapshot.Sequence;
            _upserts.Clear();
            _removals.Clear();
            _fromSequence = _toSequence = 0;
        }
        _signal.Writer.TryWrite(true);
    }

    public void QueueChange(LiveChange change)
    {
        lock (_sync)
        {
            // Already contained in the snapshot this client was given.
            if (change.Sequence <= _baseSequence) return;

            if (_fromSequence == 0) _fromSequence = change.Sequence;
            _toSequence = change.Sequence;

            foreach (var position in change.Upserted)
            {
                _upserts[position.VehicleKey] = position;
                _removals.Remove(position.VehicleKey);
            }
            foreach (var key in change.RemovedVehicleKeys)
            {
                _upserts.Remove(key);
                _removals.Add(key);
            }
        }
        _signal.Writer.TryWrite(true);
    }

    public void QueueAlertsVersion(long version)
    {
        lock (_sync)
        {
            // Never go backwards: a publish racing with Register could otherwise deliver an older number last.
            if (_pendingAlertsVersion is null || version > _pendingAlertsVersion) _pendingAlertsVersion = version;
        }
        _signal.Writer.TryWrite(true);
    }

    /// <summary>Waits until something is pending. Returns false if nothing arrived within <paramref name="timeout"/>.</summary>
    public async ValueTask<bool> WaitForWorkAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await _signal.Reader.WaitToReadAsync(timeoutSource.Token);
            _signal.Reader.TryRead(out _);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Hands over everything pending and leaves the outbox empty.</summary>
    public (LiveSnapshot? Snapshot, PendingDelta? Delta, long? AlertsVersion) Take()
    {
        lock (_sync)
        {
            var snapshot = _pendingSnapshot;
            _pendingSnapshot = null;

            PendingDelta? delta = null;
            if (_fromSequence != 0)
            {
                delta = new PendingDelta(_fromSequence, _toSequence, _upserts.Values.ToList(), _removals.ToList());
                _upserts.Clear();
                _removals.Clear();
                _fromSequence = _toSequence = 0;
            }
            var alertsVersion = _pendingAlertsVersion;
            _pendingAlertsVersion = null;
            return (snapshot, delta, alertsVersion);
        }
    }
}

internal sealed record PendingDelta(
    long FromSequence, long ToSequence, IReadOnlyList<VehiclePosition> Upserted, IReadOnlyList<string> Removed);
