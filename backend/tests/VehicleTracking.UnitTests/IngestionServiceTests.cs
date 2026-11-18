using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.Infrastructure.LiveState;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class IngestionServiceTests
{
    private readonly IFeedSource _feed = Substitute.For<IFeedSource>();
    private readonly IPositionSpool _spool = Substitute.For<IPositionSpool>();
    private readonly ILivePublisher _publisher1 = Substitute.For<ILivePublisher>();
    private readonly ILivePublisher _publisher2 = Substitute.For<ILivePublisher>();
    private readonly IIngestionMetrics _metrics = Substitute.For<IIngestionMetrics>();
    private readonly IngestionService _service;
    private readonly InMemoryLiveState _state = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = new(Now);

    public IngestionServiceTests()
    {
        var time = _time;
        _service = new IngestionService(
            _feed, new SnapshotNormalizer(), _state, _spool,
            new[] { _publisher1, _publisher2 }, _metrics,
            Options.Create(new RetentionOptions { Period = TimeSpan.FromHours(3) }), Options.Create(new LiveOptions()), time);
    }

    private void FeedReturns(params FeedVehicle[] vehicles) =>
        _feed.ReadAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(vehicles));

    [Fact]
    public async Task Appends_changed_positions_to_the_spool_publishes_them_and_records_the_read()
    {
        FeedReturns(Feed(vehicleId: "1"), Feed(vehicleId: "2"));

        await _service.RunOnceAsync(CancellationToken.None);

        await _spool.Received(1).AppendAsync(
            Arg.Is<IReadOnlyCollection<VehiclePosition>>(b => b.Count == 2), Arg.Any<CancellationToken>());
        _publisher1.Received(1).Publish(Arg.Is<LiveChange>(c => c.Upserted.Count == 2));
        _publisher2.Received(1).Publish(Arg.Is<LiveChange>(c => c.Upserted.Count == 2));
        _metrics.Received(1).RecordSuccessfulRead(
            Now, Arg.Any<TimeSpan>(), entities: 2, changed: 2, rejected: 0);
    }

    [Fact]
    public async Task Does_not_spool_or_publish_when_the_same_snapshot_is_read_twice()
    {
        FeedReturns(Feed());
        await _service.RunOnceAsync(CancellationToken.None);
        _spool.ClearReceivedCalls();
        _publisher1.ClearReceivedCalls();
        _publisher2.ClearReceivedCalls();

        await _service.RunOnceAsync(CancellationToken.None);

        await _spool.DidNotReceive().AppendAsync(
            Arg.Any<IReadOnlyCollection<VehiclePosition>>(), Arg.Any<CancellationToken>());
        _publisher1.DidNotReceive().Publish(Arg.Any<LiveChange>());
        _publisher2.DidNotReceive().Publish(Arg.Any<LiveChange>());
    }

    [Fact]
    public async Task While_the_feed_is_down_no_vehicle_leaves_the_live_map()
    {
        FeedReturns(Feed(vehicleId: "1"));
        await _service.RunOnceAsync(CancellationToken.None);

        // Half an hour of failed reads: far beyond the 10 minute live window.
        _feed.ReadAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new FeedReadException("down"));
        _time.Advance(TimeSpan.FromMinutes(30));
        await Assert.ThrowsAsync<FeedReadException>(() => _service.RunOnceAsync(CancellationToken.None));

        Assert.Single(_state.GetSnapshot().Vehicles);
    }

    [Fact]
    public async Task A_feed_failure_is_recorded_and_rethrown_without_spooling_or_publishing()
    {
        _feed.ReadAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new FeedReadException("down"));

        await Assert.ThrowsAsync<FeedReadException>(() => _service.RunOnceAsync(CancellationToken.None));

        _metrics.Received(1).RecordFailedRead();
        _metrics.DidNotReceive().RecordSuccessfulRead(
            Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        await _spool.DidNotReceive().AppendAsync(
            Arg.Any<IReadOnlyCollection<VehiclePosition>>(), Arg.Any<CancellationToken>());
        _publisher1.DidNotReceive().Publish(Arg.Any<LiveChange>());
    }

    [Fact]
    public async Task Writes_the_spool_before_calling_the_publishers()
    {
        FeedReturns(Feed());

        await _service.RunOnceAsync(CancellationToken.None);

        Received.InOrder(() =>
        {
            _spool.AppendAsync(Arg.Any<IReadOnlyCollection<VehiclePosition>>(), Arg.Any<CancellationToken>());
            _publisher1.Publish(Arg.Any<LiveChange>());
            _publisher2.Publish(Arg.Any<LiveChange>());
        });
    }
}
