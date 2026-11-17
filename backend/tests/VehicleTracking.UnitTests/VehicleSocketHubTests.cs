using NSubstitute;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class VehicleSocketHubTests
{
    private readonly ILiveState _liveState = Substitute.For<ILiveState>();
    private readonly VehicleSocketHub _hub;

    public VehicleSocketHubTests()
    {
        _liveState.GetSnapshot().Returns(new LiveSnapshot(3, new[] { Position("A:1") }));
        _hub = new VehicleSocketHub(_liveState);
    }

    private static LiveChange ChangeWith(long sequence, string key) =>
        new(sequence, new[] { Position(key) }, Array.Empty<string>());

    [Fact]
    public void Register_gives_the_new_client_the_current_snapshot()
    {
        var outbox = _hub.Register();

        var (snapshot, _) = outbox.Take();

        Assert.Equal(3, snapshot!.Sequence);
        Assert.Equal("A:1", Assert.Single(snapshot.Vehicles).VehicleKey);
    }

    [Fact]
    public void Publish_reaches_every_registered_outbox()
    {
        var first = _hub.Register();
        var second = _hub.Register();
        first.Take();
        second.Take();

        _hub.Publish(ChangeWith(4, "A:2"));

        Assert.Equal("A:2", Assert.Single(first.Take().Delta!.Upserted).VehicleKey);
        Assert.Equal("A:2", Assert.Single(second.Take().Delta!.Upserted).VehicleKey);
    }

    [Fact]
    public void After_unregister_the_outbox_no_longer_receives_changes()
    {
        var outbox = _hub.Register();
        outbox.Take();
        _hub.Unregister(outbox);

        _hub.Publish(ChangeWith(4, "A:2"));

        Assert.Null(outbox.Take().Delta);
    }

    [Fact]
    public void Connected_clients_follows_register_and_unregister()
    {
        Assert.Equal(0, _hub.ConnectedClients);

        var first = _hub.Register();
        var second = _hub.Register();
        Assert.Equal(2, _hub.ConnectedClients);

        _hub.Unregister(first);
        Assert.Equal(1, _hub.ConnectedClients);

        _hub.Unregister(second);
        Assert.Equal(0, _hub.ConnectedClients);
    }
}
