using VehicleTracking.Application.Live;
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
        var change = _state.Apply(new[] { Position("A:1"), Position("A:2") }, NoEviction, NoEviction);

        Assert.Equal(1, change.Sequence);
        Assert.Equal(2, change.Upserted.Count);
        Assert.Empty(change.RemovedVehicleKeys);
    }

    [Fact]
    public void Ignores_a_candidate_that_is_not_newer_than_the_known_position()
    {
        _state.Apply(new[] { Position("A:1", at: Now) }, NoEviction, NoEviction);

        var sameTime = _state.Apply(new[] { Position("A:1", at: Now, lat: 60) }, NoEviction, NoEviction);
        var older = _state.Apply(new[] { Position("A:1", at: Now.AddSeconds(-5), lat: 61) }, NoEviction, NoEviction);

        Assert.True(sameTime.IsEmpty);
        Assert.True(older.IsEmpty);
        Assert.Equal(59.9, Assert.Single(_state.GetSnapshot().Vehicles).Latitude);
    }

    [Fact]
    public void An_apply_that_changes_nothing_does_not_advance_the_sequence()
    {
        _state.Apply(new[] { Position("A:1") }, NoEviction, NoEviction);

        var change = _state.Apply(new[] { Position("A:1") }, NoEviction, NoEviction);

        Assert.True(change.IsEmpty);
        Assert.Equal(1, change.Sequence);
    }

    [Fact]
    public void Ignores_a_candidate_older_than_evict_before()
    {
        var change = _state.Apply(new[] { Position("A:1", at: Now.AddHours(-4)) }, retainAfter: Now.AddHours(-3), liveAfter: Now.AddHours(-3));

        Assert.True(change.IsEmpty);
        Assert.Empty(_state.GetSnapshot().Vehicles);
    }

    [Fact]
    public void Removes_a_stored_vehicle_older_than_evict_before_and_reports_it()
    {
        _state.Apply(new[] { Position("A:1", at: Now.AddHours(-2)), Position("A:2", at: Now) }, NoEviction, NoEviction);

        var change = _state.Apply(Array.Empty<Domain.Vehicles.VehiclePosition>(), retainAfter: Now.AddHours(-1), liveAfter: Now.AddHours(-1));

        Assert.Equal(new[] { "A:1" }, change.RemovedVehicleKeys);
        Assert.Equal(2, change.Sequence);
        Assert.Equal("A:2", Assert.Single(_state.GetSnapshot().Vehicles).VehicleKey);
    }

    [Fact]
    public void Snapshot_returns_all_vehicles_with_the_current_sequence()
    {
        _state.Apply(new[] { Position("A:1"), Position("A:2") }, NoEviction, NoEviction);
        _state.Apply(new[] { Position("A:3") }, NoEviction, NoEviction);

        var snapshot = _state.GetSnapshot();

        Assert.Equal(2, snapshot.Sequence);
        Assert.Equal(3, snapshot.Vehicles.Count);
    }

    [Fact]
    public void A_silent_vehicle_leaves_the_live_view_but_is_still_remembered()
    {
        _state.Apply(new[] { Position("A:1", at: Now.AddMinutes(-15)), Position("A:2", at: Now) }, NoEviction, NoEviction);

        var change = _state.Apply(Array.Empty<Domain.Vehicles.VehiclePosition>(), retainAfter: Now.AddHours(-3), liveAfter: Now.AddMinutes(-10));

        Assert.Equal(new[] { "A:1" }, change.RemovedVehicleKeys);
        Assert.Equal(new[] { "A:2" }, _state.GetSnapshot().Vehicles.Select(v => v.VehicleKey));

        // The feed repeating the silent vehicle's last position is recognised: nothing to store again.
        var repeated = _state.Apply(new[] { Position("A:1", at: Now.AddMinutes(-15)) }, Now.AddHours(-3), Now.AddMinutes(-10));
        Assert.Empty(repeated.StoredPositions);
        Assert.True(repeated.IsEmpty);
    }

    [Fact]
    public void A_position_too_old_to_show_is_still_stored()
    {
        // e.g. after a restart, the feed still carries a position from 15 minutes ago that was never saved
        var change = _state.Apply(new[] { Position("A:1", at: Now.AddMinutes(-15)) }, retainAfter: Now.AddHours(-3), liveAfter: Now.AddMinutes(-10));

        Assert.Single(change.StoredPositions);
        Assert.Empty(change.Upserted);
        Assert.Empty(_state.GetSnapshot().Vehicles);
    }

    [Fact]
    public void A_silent_vehicle_that_reports_again_comes_back()
    {
        _state.Apply(new[] { Position("A:1", at: Now.AddMinutes(-15)) }, Now.AddHours(-3), Now.AddMinutes(-10));

        var change = _state.Apply(new[] { Position("A:1", at: Now) }, Now.AddHours(-3), Now.AddMinutes(-10));

        Assert.Single(change.Upserted);
        Assert.Single(_state.GetSnapshot().Vehicles);
    }

    [Fact]
    public void Remembered_vehicles_are_grouped_by_the_age_of_their_last_report()
    {
        _state.Apply(
            new[]
            {
                Position("A:1", at: Now.AddSeconds(-20)),
                Position("A:2", at: Now.AddMinutes(-3)),
                Position("A:3", at: Now.AddMinutes(-7)),
                Position("A:4", at: Now.AddMinutes(-40)),
            },
            retainAfter: Now.AddHours(-3),
            liveAfter: Now.AddMinutes(-10));

        var freshness = _state.CountByAge(Now, TimeSpan.FromMinutes(10));

        Assert.Equal(new LiveFreshness(1, 1, 1, 1, TimeSpan.FromMinutes(10)), freshness);
    }
}
