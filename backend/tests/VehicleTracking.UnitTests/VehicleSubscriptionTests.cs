using System.Text.Json;
using VehicleTracking.Api.Realtime;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class VehicleSubscriptionTests
{
    private static bool Parse(string json, out VehicleSubscription sub)
    {
        using var doc = JsonDocument.Parse(json);
        return VehicleSubscription.TryParse(doc.RootElement, out sub);
    }

    private static VehicleSubscription Sub(string json)
    {
        Assert.True(Parse(json, out var sub), json);
        return sub;
    }

    [Fact]
    public void Everything_matches_every_vehicle()
    {
        Assert.True(VehicleSubscription.Everything.Matches(Position(lat: -50, lon: 100)));
    }

    [Fact]
    public void Empty_message_means_everything()
    {
        var sub = Sub("""{"type":"subscribe"}""");
        Assert.Null(sub.Bbox);
        Assert.Empty(sub.Lines);
        Assert.Empty(sub.Keys);
        Assert.True(sub.Matches(Position()));
    }

    [Fact]
    public void Bbox_edges_are_inclusive()
    {
        var sub = Sub("""{"bbox":[10,59,11,60]}""");
        Assert.True(sub.Matches(Position(lat: 59, lon: 10)));
        Assert.True(sub.Matches(Position(lat: 60, lon: 11)));
        Assert.False(sub.Matches(Position(lat: 60.0001, lon: 10.5)));
        Assert.False(sub.Matches(Position(lat: 59.5, lon: 11.0001)));
    }

    [Fact]
    public void Lines_win_over_bbox()
    {
        var sub = Sub("""{"bbox":[10,59,11,60],"lines":["RUT:Line:31"]}""");
        Assert.True(sub.Matches(Position(lat: 0, lon: 0, routeId: "RUT:Line:31")));
        Assert.False(sub.Matches(Position(lat: 59.5, lon: 10.5, routeId: "RUT:Line:1")));
        Assert.False(sub.Matches(Position(lat: 59.5, lon: 10.5, routeId: null)));
    }

    [Fact]
    public void Keys_win_over_lines_and_bbox()
    {
        var sub = Sub("""{"bbox":[10,59,11,60],"lines":["X"],"keys":["RUT:7"]}""");
        Assert.True(sub.Matches(Position("RUT:7", lat: 0, lon: 0, routeId: "other")));
        Assert.False(sub.Matches(Position("RUT:8", lat: 59.5, lon: 10.5, routeId: "other")));
    }

    [Theory]
    [InlineData("""{"bbox":[10,59,11]}""")]
    [InlineData("""{"bbox":[10,59,11,60,1]}""")]
    [InlineData("""{"bbox":["a",59,11,60]}""")]
    [InlineData("""{"bbox":"10,59,11,60"}""")]
    [InlineData("""{"bbox":[11,59,10,60]}""")]
    [InlineData("""{"bbox":[10,60,11,59]}""")]
    [InlineData("""{"bbox":[10,59,10,60]}""")]
    [InlineData("""{"bbox":[10,-91,11,60]}""")]
    [InlineData("""{"bbox":[10,59,11,91]}""")]
    [InlineData("""{"bbox":[-181,59,11,60]}""")]
    [InlineData("""{"bbox":[10,59,181,60]}""")]
    [InlineData("""{"bbox":[1e999,59,11,60]}""")]
    [InlineData("""{"lines":"RUT:Line:31"}""")]
    [InlineData("""{"lines":[1,2]}""")]
    [InlineData("""{"keys":{"a":1}}""")]
    public void Invalid_shapes_are_rejected(string json)
    {
        Assert.False(Parse(json, out _));
    }

    [Fact]
    public void Non_object_is_rejected()
    {
        Assert.False(Parse("[1,2]", out _));
        Assert.False(Parse("\"subscribe\"", out _));
    }

    [Fact]
    public void Null_fields_mean_not_set()
    {
        var sub = Sub("""{"bbox":null,"lines":null,"keys":null}""");
        Assert.Null(sub.Bbox);
    }

    [Fact]
    public void Line_and_key_limits_are_enforced()
    {
        string Array(int n) => "[" + string.Join(",", Enumerable.Range(0, n).Select(i => $"\"id:{i}\"")) + "]";

        Assert.True(Parse($$"""{"lines":{{Array(500)}}}""", out _));
        Assert.False(Parse($$"""{"lines":{{Array(501)}}}""", out _));
        Assert.True(Parse($$"""{"keys":{{Array(20)}}}""", out _));
        Assert.False(Parse($$"""{"keys":{{Array(21)}}}""", out _));
    }

    [Fact]
    public void Query_values_are_parsed_and_keep_colons()
    {
        Assert.True(VehicleSubscription.TryFromQueryValues(
            "10.5,59.8,10.9,60.0", "RUT:Line:31,RUT:Line:2", "RUT:101234", out var sub));

        Assert.Equal(new BoundingBox(10.5, 59.8, 10.9, 60.0), sub.Bbox);
        Assert.Equal(new[] { "RUT:Line:2", "RUT:Line:31" }, sub.Lines.Order());
        Assert.Equal("RUT:101234", Assert.Single(sub.Keys));
    }

    [Fact]
    public void Missing_query_values_mean_everything()
    {
        Assert.True(VehicleSubscription.TryFromQueryValues(null, "", null, out var sub));
        Assert.Null(sub.Bbox);
        Assert.Empty(sub.Lines);
        Assert.Empty(sub.Keys);
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("a,b,c,d")]
    [InlineData("11,59,10,60")]
    [InlineData("10,59,11,95")]
    public void Invalid_query_bbox_is_rejected(string bbox)
    {
        Assert.False(VehicleSubscription.TryFromQueryValues(bbox, null, null, out _));
    }

    [Fact]
    public void Too_many_query_keys_are_rejected()
    {
        var keys = string.Join(",", Enumerable.Range(0, 21).Select(i => $"k:{i}"));
        Assert.False(VehicleSubscription.TryFromQueryValues(null, null, keys, out _));
    }
}
