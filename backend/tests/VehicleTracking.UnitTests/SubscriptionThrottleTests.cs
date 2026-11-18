using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Api.Realtime;

namespace VehicleTracking.UnitTests;

public class SubscriptionThrottleTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private readonly FakeTimeProvider _time = new();
    private readonly List<VehicleSubscription> _applied = new();

    private static VehicleSubscription Keyed(string key) =>
        new(null, new HashSet<string>(), new HashSet<string> { key });

    private SubscriptionThrottle Create() => new(_time, Interval, _applied.Add);

    [Fact]
    public void First_subscription_is_applied_immediately()
    {
        using var throttle = Create();
        var first = Keyed("a");

        throttle.Submit(first);

        Assert.Same(first, Assert.Single(_applied));
    }

    [Fact]
    public void Early_subscription_waits_and_only_the_latest_is_applied()
    {
        using var throttle = Create();
        throttle.Submit(Keyed("a"));
        _time.Advance(TimeSpan.FromMilliseconds(50));

        throttle.Submit(Keyed("b"));
        throttle.Submit(Keyed("c"));
        Assert.Single(_applied);

        _time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.Single(_applied);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, _applied.Count);
        Assert.Contains("c", _applied[1].Keys);
    }

    [Fact]
    public void Subscription_after_the_interval_is_applied_immediately()
    {
        using var throttle = Create();
        throttle.Submit(Keyed("a"));
        _time.Advance(Interval);

        throttle.Submit(Keyed("b"));

        Assert.Equal(2, _applied.Count);
    }

    [Fact]
    public void Disposed_throttle_applies_nothing_more()
    {
        var throttle = Create();
        throttle.Submit(Keyed("a"));
        throttle.Submit(Keyed("b"));
        throttle.Dispose();

        _time.Advance(Interval);

        Assert.Single(_applied);
    }
}
