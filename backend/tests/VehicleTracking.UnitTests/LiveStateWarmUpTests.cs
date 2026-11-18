using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.Infrastructure.LiveState;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class LiveStateWarmUpTests
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(3);

    private readonly ILatestPositionReader _reader = Substitute.For<ILatestPositionReader>();
    private readonly ILivePublisher _publisher = Substitute.For<ILivePublisher>();
    private readonly InMemoryLiveState _liveState = new();
    private readonly LiveStateWarmUp _warmUp;

    public LiveStateWarmUpTests()
    {
        _warmUp = new LiveStateWarmUp(
            _reader, _liveState, new[] { _publisher },
            Options.Create(new RetentionOptions { Period = Retention }), Options.Create(new LiveOptions()), new FakeTimeProvider(Now));
    }

    private void DatabaseHolds(params VehiclePosition[] positions) =>
        _reader.ReadLatestPerVehicleAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(positions);

    [Fact]
    public async Task Restores_the_stored_positions_into_the_live_state_and_publishes_them()
    {
        DatabaseHolds(Position("ATB:1", Now.AddMinutes(-1)), Position("ATB:2", Now.AddMinutes(-2)));

        var restored = await _warmUp.RunAsync(CancellationToken.None);

        Assert.Equal(2, restored);
        Assert.Equal(2, _liveState.GetSnapshot().Vehicles.Count);
        _publisher.Received(1).Publish(Arg.Is<LiveChange>(change => change.Upserted.Count == 2));
    }

    [Fact]
    public async Task Asks_the_database_only_for_the_retention_window()
    {
        DatabaseHolds();

        await _warmUp.RunAsync(CancellationToken.None);

        await _reader.Received(1).ReadLatestPerVehicleAsync(Now - Retention, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_overwrite_a_fresher_position_that_the_feed_already_delivered()
    {
        // The feed poller and the warm-up start at the same time; the poller may win the race.
        var fromFeed = Position("ATB:1", Now.AddSeconds(-5), lat: 63.5);
        _liveState.Apply(new[] { fromFeed }, retainAfter: Now - Retention, liveAfter: Now - new LiveOptions().StaleAfter);
        DatabaseHolds(Position("ATB:1", Now.AddMinutes(-10), lat: 63.0));

        var restored = await _warmUp.RunAsync(CancellationToken.None);

        Assert.Equal(0, restored);
        Assert.Equal(63.5, Assert.Single(_liveState.GetSnapshot().Vehicles).Latitude);
    }

    [Fact]
    public async Task Publishes_nothing_when_there_is_nothing_to_restore()
    {
        DatabaseHolds();

        await _warmUp.RunAsync(CancellationToken.None);

        _publisher.DidNotReceive().Publish(Arg.Any<LiveChange>());
    }
}
