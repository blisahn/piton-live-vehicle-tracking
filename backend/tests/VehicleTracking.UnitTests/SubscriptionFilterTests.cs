using VehicleTracking.Api.Realtime;
using VehicleTracking.Domain.Vehicles;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class SubscriptionFilterTests
{
    private static readonly VehicleSubscription Area =
        new(new BoundingBox(10, 59, 11, 60), new HashSet<string>(), new HashSet<string>());

    private static VehiclePosition Inside(string key) => Position(key, lat: 59.5, lon: 10.5);
    private static VehiclePosition Outside(string key) => Position(key, lat: 40, lon: 0);

    [Fact]
    public void Snapshot_keeps_only_matching_vehicles()
    {
        var filter = new SubscriptionFilter();

        var result = filter.FilterSnapshot(new[] { Inside("A"), Outside("B") }, Area);

        Assert.Equal("A", Assert.Single(result).VehicleKey);
        Assert.Equal(1, filter.SentCount);
    }

    [Fact]
    public void Snapshot_resets_what_was_sent()
    {
        var filter = new SubscriptionFilter();
        filter.FilterSnapshot(new[] { Inside("A") }, Area);

        filter.FilterSnapshot(new[] { Inside("B") }, Area);
        var (_, removed) = filter.FilterDelta([], ["A"], Area);

        Assert.Empty(removed);
    }

    [Fact]
    public void Vehicle_entering_the_area_is_upserted()
    {
        var filter = new SubscriptionFilter();
        filter.FilterSnapshot(new[] { Outside("A") }, Area);

        var (upserted, removed) = filter.FilterDelta(new[] { Inside("A") }, [], Area);

        Assert.Equal("A", Assert.Single(upserted).VehicleKey);
        Assert.Empty(removed);
    }

    [Fact]
    public void Vehicle_leaving_the_area_becomes_a_removal()
    {
        var filter = new SubscriptionFilter();
        filter.FilterSnapshot(new[] { Inside("A") }, Area);

        var (upserted, removed) = filter.FilterDelta(new[] { Outside("A") }, [], Area);

        Assert.Empty(upserted);
        Assert.Equal("A", Assert.Single(removed));

        // Gone from the sent set: a later removal is not repeated.
        Assert.Empty(filter.FilterDelta([], ["A"], Area).Removed);
    }

    [Fact]
    public void Removal_of_a_vehicle_never_sent_is_dropped()
    {
        var filter = new SubscriptionFilter();
        filter.FilterSnapshot(new[] { Inside("A") }, Area);

        var (_, removed) = filter.FilterDelta([], ["A", "never-sent"], Area);

        Assert.Equal("A", Assert.Single(removed));
    }

    [Fact]
    public void Outside_vehicle_that_was_never_sent_produces_nothing()
    {
        var filter = new SubscriptionFilter();

        var (upserted, removed) = filter.FilterDelta(new[] { Outside("A") }, [], Area);

        Assert.Empty(upserted);
        Assert.Empty(removed);
    }

    [Fact]
    public void Everything_subscription_passes_all_through()
    {
        var filter = new SubscriptionFilter();

        var (upserted, _) = filter.FilterDelta(new[] { Inside("A"), Outside("B") }, [], VehicleSubscription.Everything);

        Assert.Equal(2, upserted.Count);
    }
}
