using VehicleTracking.Infrastructure.LiveState;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class InMemoryLiveStateTests
{
    private static readonly DateTimeOffset NoEviction = DateTimeOffset.MinValue;
    private readonly InMemoryLiveState _state = new();

    [Fact]
    public void First_apply_upserts_everything_and_returns_sequence_one()
    {
        var change = _state.Apply(new[] { Position("A:1"), Position("A:2") }, NoEviction);

        Assert.Equal(1, change.Sequence);
        Assert.Equal(2, change.Upserted.Count);
        Assert.Empty(change.RemovedVehicleKeys);
    }

    [Fact]
    public void Ignores_a_candidate_that_is_not_newer_than_the_known_position()
    {
        _state.Apply(new[] { Position("A:1", at: Now) }, NoEviction);

        var sameTime = _state.Apply(new[] { Position("A:1", at: Now, lat: 60) }, NoEviction);
        var older = _state.Apply(new[] { Position("A:1", at: Now.AddSeconds(-5), lat: 61) }, NoEviction);

        Assert.True(sameTime.IsEmpty);
        Assert.True(older.IsEmpty);
        Assert.Equal(59.9, Assert.Single(_state.GetSnapshot().Vehicles).Latitude);
    }

    [Fact]
    public void An_apply_that_changes_nothing_does_not_advance_the_sequence()
    {
        _state.Apply(new[] { Position("A:1") }, NoEviction);

        var change = _state.Apply(new[] { Position("A:1") }, NoEviction);

        Assert.True(change.IsEmpty);
        Assert.Equal(1, change.Sequence);
    }

    [Fact]
    public void Ignores_a_candidate_older_than_evict_before()
    {
        var change = _state.Apply(new[] { Position("A:1", at: Now.AddHours(-4)) }, evictBefore: Now.AddHours(-3));

        Assert.True(change.IsEmpty);
        Assert.Empty(_state.GetSnapshot().Vehicles);
    }

    [Fact]
    public void Removes_a_stored_vehicle_older_than_evict_before_and_reports_it()
    {
        _state.Apply(new[] { Position("A:1", at: Now.AddHours(-2)), Position("A:2", at: Now) }, NoEviction);

        var change = _state.Apply(Array.Empty<Domain.Vehicles.VehiclePosition>(), evictBefore: Now.AddHours(-1));

        Assert.Equal(new[] { "A:1" }, change.RemovedVehicleKeys);
        Assert.Equal(2, change.Sequence);
        Assert.Equal("A:2", Assert.Single(_state.GetSnapshot().Vehicles).VehicleKey);
    }

    [Fact]
    public void Snapshot_returns_all_vehicles_with_the_current_sequence()
    {
        _state.Apply(new[] { Position("A:1"), Position("A:2") }, NoEviction);
        _state.Apply(new[] { Position("A:3") }, NoEviction);

        var snapshot = _state.GetSnapshot();

        Assert.Equal(2, snapshot.Sequence);
        Assert.Equal(3, snapshot.Vehicles.Count);
    }
}
