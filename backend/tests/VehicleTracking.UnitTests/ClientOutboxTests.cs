using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class ClientOutboxTests
{
    private readonly ClientOutbox _outbox = new();

    private static LiveChange Change(long sequence, VehiclePosition[]? upserted = null, string[]? removed = null) =>
        new(sequence, upserted ?? Array.Empty<VehiclePosition>(), removed ?? Array.Empty<string>());

    private static LiveSnapshot SnapshotAt(long sequence, params VehiclePosition[] vehicles) => new(sequence, vehicles);

    [Fact]
    public void After_a_snapshot_take_returns_the_snapshot_and_no_delta()
    {
        var snapshot = SnapshotAt(5, Position("A:1"));
        _outbox.QueueSnapshot(snapshot);

        var (taken, delta) = _outbox.Take();

        Assert.Same(snapshot, taken);
        Assert.Null(delta);
    }

    [Fact]
    public void Changes_after_a_snapshot_are_merged_into_one_delta_with_the_latest_position_per_vehicle()
    {
        _outbox.QueueSnapshot(SnapshotAt(5));
        _outbox.QueueChange(Change(6, new[] { Position("A:1", lat: 1) }));
        _outbox.QueueChange(Change(7, new[] { Position("A:1", lat: 2), Position("A:2") }));

        var (_, delta) = _outbox.Take();

        Assert.NotNull(delta);
        Assert.Equal(6, delta.FromSequence);
        Assert.Equal(7, delta.ToSequence);
        Assert.Equal(2, delta.Upserted.Count);
        Assert.Equal(2, delta.Upserted.Single(p => p.VehicleKey == "A:1").Latitude);
    }

    [Fact]
    public void A_change_not_newer_than_the_snapshot_is_skipped_because_the_snapshot_contains_it()
    {
        _outbox.QueueSnapshot(SnapshotAt(5));
        _outbox.QueueChange(Change(5, new[] { Position("A:1") }));
        _outbox.QueueChange(Change(4, new[] { Position("A:2") }));

        var (_, delta) = _outbox.Take();

        Assert.Null(delta);
    }

    [Fact]
    public void An_upsert_followed_by_a_removal_of_the_same_vehicle_ends_up_only_in_removed()
    {
        _outbox.QueueChange(Change(1, new[] { Position("A:1") }));
        _outbox.QueueChange(Change(2, removed: new[] { "A:1" }));

        var (_, delta) = _outbox.Take();

        Assert.Empty(delta!.Upserted);
        Assert.Equal(new[] { "A:1" }, delta.Removed);
    }

    [Fact]
    public void A_removal_followed_by_an_upsert_of_the_same_vehicle_ends_up_only_in_upserted()
    {
        _outbox.QueueChange(Change(1, removed: new[] { "A:1" }));
        _outbox.QueueChange(Change(2, new[] { Position("A:1") }));

        var (_, delta) = _outbox.Take();

        Assert.Empty(delta!.Removed);
        Assert.Equal("A:1", Assert.Single(delta.Upserted).VehicleKey);
    }

    [Fact]
    public void A_new_snapshot_discards_the_changes_that_were_pending()
    {
        _outbox.QueueChange(Change(1, new[] { Position("A:1") }));

        _outbox.QueueSnapshot(SnapshotAt(3));
        var (snapshot, delta) = _outbox.Take();

        Assert.NotNull(snapshot);
        Assert.Null(delta);
    }

    [Fact]
    public void Take_empties_the_outbox()
    {
        _outbox.QueueSnapshot(SnapshotAt(1));
        _outbox.QueueChange(Change(2, new[] { Position("A:1") }));
        _outbox.Take();

        var (snapshot, delta) = _outbox.Take();

        Assert.Null(snapshot);
        Assert.Null(delta);
    }

    [Fact]
    public async Task Wait_for_work_returns_true_when_something_is_queued()
    {
        _outbox.QueueChange(Change(1, new[] { Position("A:1") }));

        Assert.True(await _outbox.WaitForWorkAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task Wait_for_work_returns_false_after_the_timeout_when_nothing_is_queued()
    {
        Assert.False(await _outbox.WaitForWorkAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None));
    }
}
