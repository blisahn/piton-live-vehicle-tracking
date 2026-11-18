namespace VehicleTracking.Api.Realtime;

/// <summary>
/// Applies at most one subscription per interval. A subscription that arrives too early is held;
/// a newer one replaces it, and the latest is applied when the interval has passed, so the final
/// state is never lost.
/// </summary>
internal sealed class SubscriptionThrottle(TimeProvider time, TimeSpan interval, Action<VehicleSubscription> apply) : IDisposable
{
    private readonly object _sync = new();
    private long? _lastApplied;
    private VehicleSubscription? _pending;
    private ITimer? _timer;
    private bool _disposed;

    public void Submit(VehicleSubscription subscription)
    {
        lock (_sync)
        {
            if (_disposed) return;
            var now = time.GetTimestamp();
            var wait = _lastApplied is { } last ? interval - time.GetElapsedTime(last, now) : TimeSpan.Zero;
            if (wait <= TimeSpan.Zero && _pending is null)
            {
                _lastApplied = now;
                apply(subscription);
                return;
            }

            _pending = subscription;
            _timer ??= time.CreateTimer(_ => Flush(), null, wait > TimeSpan.Zero ? wait : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
            if (_disposed || _pending is null) return;
            var subscription = _pending;
            _pending = null;
            _lastApplied = time.GetTimestamp();
            apply(subscription);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
