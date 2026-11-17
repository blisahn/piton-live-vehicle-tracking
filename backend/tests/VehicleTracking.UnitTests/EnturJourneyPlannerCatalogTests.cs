using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Infrastructure.Transit;

namespace VehicleTracking.UnitTests;

public class EnturJourneyPlannerCatalogTests
{
    private sealed class CannedHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (EnturJourneyPlannerCatalog Catalog, CannedHandler Handler) CatalogAnswering(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new CannedHandler(status, json);
        var options = Options.Create(new JourneyPlannerOptions { Url = "http://journey-planner.test/graphql" });
        return (new EnturJourneyPlannerCatalog(new HttpClient(handler), options), handler);
    }

    [Fact]
    public async Task Lines_are_parsed_and_unknown_ids_are_skipped()
    {
        var (catalog, _) = CatalogAnswering("""
            {"data":{"lines":[
                {"id":"AKT:Line:1","publicCode":"M1","name":"Flekkerøy","transportMode":"bus"},
                null,
                {"id":"AKT:Line:2","publicCode":null,"name":null,"transportMode":"rail"}
            ]}}
            """);

        var lines = await catalog.GetLinesAsync(["AKT:Line:1", "unknown", "AKT:Line:2"], CancellationToken.None);

        Assert.Equal(2, lines.Count);
        Assert.Equal("M1", lines[0].PublicCode);
        Assert.Null(lines[1].Name);
    }

    [Fact]
    public async Task Graphql_errors_become_a_catalog_exception()
    {
        var (catalog, _) = CatalogAnswering("""{"errors":[{"message":"boom"}]}""");

        await Assert.ThrowsAsync<TransitCatalogException>(() => catalog.GetLinesAsync(["a"], CancellationToken.None));
    }

    [Fact]
    public async Task A_failing_http_status_becomes_a_catalog_exception()
    {
        var (catalog, _) = CatalogAnswering("{}", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<TransitCatalogException>(() => catalog.GetStopsAsync(["a"], CancellationToken.None));
    }

    [Fact]
    public async Task A_trip_without_a_drawn_path_falls_back_to_the_stops()
    {
        var (catalog, _) = CatalogAnswering("""
            {"data":{"serviceJourney":{
                "id":"ATB:ServiceJourney:1",
                "line":{"id":"ATB:Line:1","publicCode":"1","name":"One","transportMode":"bus"},
                "pointsOnLink":null,
                "quays":[
                    {"id":"NSR:Quay:1","name":"First","latitude":63.4,"longitude":10.4},
                    {"id":"NSR:Quay:2","name":"Second","latitude":63.5,"longitude":10.5}
                ]
            }}}
            """);

        var route = await catalog.GetTripRouteAsync("ATB:ServiceJourney:1", CancellationToken.None);

        Assert.NotNull(route);
        Assert.True(route.PathIsApproximate);
        Assert.Equal(2, route.Path.Count);
        Assert.Equal(63.5, route.Path[1].Latitude);
    }

    [Fact]
    public async Task A_trip_with_a_drawn_path_uses_it()
    {
        var (catalog, _) = CatalogAnswering("""
            {"data":{"serviceJourney":{
                "id":"t","line":null,
                "pointsOnLink":{"length":3,"points":"_p~iF~ps|U_ulLnnqC_mqNvxq`@"},
                "quays":[]
            }}}
            """);

        var route = await catalog.GetTripRouteAsync("t", CancellationToken.None);

        Assert.NotNull(route);
        Assert.False(route.PathIsApproximate);
        Assert.Equal(3, route.Path.Count);
    }

    [Fact]
    public async Task A_path_with_no_more_points_than_stops_is_flagged_as_approximate()
    {
        // Three path points for three stops: just the stations joined by straight lines.
        var (catalog, _) = CatalogAnswering("""
            {"data":{"serviceJourney":{
                "id":"t","line":null,
                "pointsOnLink":{"length":3,"points":"_p~iF~ps|U_ulLnnqC_mqNvxq`@"},
                "quays":[
                    {"id":"a","name":"A","latitude":38.5,"longitude":-120.2},
                    {"id":"b","name":"B","latitude":40.7,"longitude":-120.95},
                    {"id":"c","name":"C","latitude":43.252,"longitude":-126.453}
                ]
            }}}
            """);

        var route = await catalog.GetTripRouteAsync("t", CancellationToken.None);

        Assert.NotNull(route);
        Assert.True(route.PathIsApproximate);
    }

    [Fact]
    public async Task An_unknown_trip_gives_null()
    {
        var (catalog, _) = CatalogAnswering("""{"data":{"serviceJourney":null}}""");

        Assert.Null(await catalog.GetTripRouteAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task Many_ids_are_split_into_requests_of_at_most_500()
    {
        var (catalog, handler) = CatalogAnswering("""{"data":{"lines":[]}}""");
        var ids = Enumerable.Range(0, 1200).Select(i => $"AKT:Line:{i}").ToList();

        await catalog.GetLinesAsync(ids, CancellationToken.None);

        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task No_ids_means_no_request()
    {
        var (catalog, handler) = CatalogAnswering("{}");

        var lines = await catalog.GetLinesAsync([], CancellationToken.None);

        Assert.Empty(lines);
        Assert.Equal(0, handler.RequestCount);
    }
}
