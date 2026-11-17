using VehicleTracking.Application.Ingestion;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class SnapshotNormalizerTests
{
    private readonly SnapshotNormalizer _normalizer = new();

    private NormalizedSnapshot Normalize(params Application.Abstractions.FeedVehicle[] vehicles) =>
        _normalizer.Normalize(Snapshot(vehicles), Now);

    [Fact]
    public void Keeps_only_the_newest_position_of_a_vehicle()
    {
        var older = Feed(entityId: "RUT:a", timestamp: Now.AddSeconds(-30), lat: 59.1);
        var newest = Feed(entityId: "RUT:b", timestamp: Now.AddSeconds(-5), lat: 59.2);
        var middle = Feed(entityId: "RUT:c", timestamp: Now.AddSeconds(-15), lat: 59.3);

        var result = Normalize(older, newest, middle);

        var position = Assert.Single(result.Positions);
        Assert.Equal(59.2, position.Latitude);
    }

    [Fact]
    public void Builds_the_vehicle_key_from_codespace_and_vehicle_id()
    {
        var result = Normalize(Feed(entityId: "RUT:ServiceJourney:1-abc", vehicleId: "101"));

        Assert.Equal("RUT:101", Assert.Single(result.Positions).VehicleKey);
    }

    [Fact]
    public void Keeps_two_operators_with_the_same_vehicle_id_as_two_vehicles()
    {
        var result = Normalize(
            Feed(entityId: "RUT:x", vehicleId: "101"),
            Feed(entityId: "ATB:y", vehicleId: "101"));

        Assert.Equal(new[] { "ATB:101", "RUT:101" }, result.Positions.Select(p => p.VehicleKey).Order());
    }

    [Fact]
    public void Uses_just_the_vehicle_id_when_the_entity_id_has_no_colon()
    {
        var result = Normalize(Feed(entityId: "nocolon", vehicleId: "101"));

        Assert.Equal("101", Assert.Single(result.Positions).VehicleKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Rejects_an_entity_without_a_vehicle_id(string? vehicleId) =>
        AssertRejected(Feed(vehicleId: vehicleId));

    [Fact]
    public void Rejects_an_entity_without_a_timestamp() => AssertRejected(Feed(noTimestamp: true));

    [Fact]
    public void Rejects_an_entity_without_latitude() => AssertRejected(Feed(lat: null));

    [Fact]
    public void Rejects_an_entity_without_longitude() => AssertRejected(Feed(lon: null));

    [Fact]
    public void Rejects_the_zero_zero_coordinates() => AssertRejected(Feed(lat: 0, lon: 0));

    // The real feed uses (-1, -1) as a "no GPS fix" placeholder.
    [Fact]
    public void Rejects_the_minus_one_minus_one_coordinates() => AssertRejected(Feed(lat: -1, lon: -1));

    [Theory]
    [InlineData(91, 10)]
    [InlineData(-91, 10)]
    [InlineData(59, 181)]
    [InlineData(59, -181)]
    public void Rejects_out_of_range_coordinates(double lat, double lon) => AssertRejected(Feed(lat: lat, lon: lon));

    [Fact]
    public void Rejects_a_timestamp_more_than_a_minute_in_the_future() =>
        AssertRejected(Feed(timestamp: Now.AddSeconds(61)));

    [Fact]
    public void Accepts_a_timestamp_a_few_seconds_in_the_future()
    {
        var result = Normalize(Feed(timestamp: Now.AddSeconds(5)));

        Assert.Single(result.Positions);
        Assert.Equal(0, result.RejectedCount);
    }

    [Fact]
    public void Counts_every_input_entity_and_every_rejected_one()
    {
        var result = Normalize(Feed(), Feed(vehicleId: null), Feed(lat: 0, lon: 0), Feed(noTimestamp: true));

        Assert.Equal(4, result.EntityCount);
        Assert.Equal(3, result.RejectedCount);
    }

    [Theory]
    [InlineData(-142f, 218f)]
    [InlineData(360f, 0f)]
    [InlineData(45f, 45f)]
    public void Normalises_bearing_into_zero_to_360(float input, float expected)
    {
        var result = Normalize(Feed(bearing: input));

        Assert.Equal(expected, Assert.Single(result.Positions).Bearing);
    }

    [Fact]
    public void Keeps_a_missing_bearing_missing()
    {
        var result = Normalize(Feed(bearing: null));

        Assert.Null(Assert.Single(result.Positions).Bearing);
    }

    [Fact]
    public void Rounds_coordinates_to_six_decimals()
    {
        var result = Normalize(Feed(lat: 59.12345678, lon: 10.98765432));

        var position = Assert.Single(result.Positions);
        Assert.Equal(59.123457, position.Latitude);
        Assert.Equal(10.987654, position.Longitude);
    }

    [Fact]
    public void Turns_empty_route_trip_and_stop_into_null()
    {
        var result = Normalize(Feed(routeId: "", tripId: "", stopId: ""));

        var position = Assert.Single(result.Positions);
        Assert.Null(position.RouteId);
        Assert.Null(position.TripId);
        Assert.Null(position.StopId);
    }

    private void AssertRejected(Application.Abstractions.FeedVehicle vehicle)
    {
        var result = Normalize(vehicle);

        Assert.Empty(result.Positions);
        Assert.Equal(1, result.RejectedCount);
        Assert.Equal(1, result.EntityCount);
    }

    [Fact]
    public void Does_not_repeat_the_codespace_when_the_vehicle_id_already_starts_with_it()
    {
        var result = new SnapshotNormalizer().Normalize(
            Snapshot(Feed(entityId: "MOR:ServiceJourney:1-a", vehicleId: "MOR:Vehicle:238_3520")), Now);

        Assert.Equal("MOR:Vehicle:238_3520", Assert.Single(result.Positions).VehicleKey);
    }
}
