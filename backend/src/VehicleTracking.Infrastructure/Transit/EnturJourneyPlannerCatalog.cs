using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Transit;

namespace VehicleTracking.Infrastructure.Transit;

/// <summary>Looks up line names, stop names and planned routes in Entur's Journey Planner (GraphQL).</summary>
internal sealed class EnturJourneyPlannerCatalog(HttpClient http, IOptions<JourneyPlannerOptions> options) : ITransitCatalog
{
    /// <summary>Keeps each request small; a full feed snapshot needs only a few of them.</summary>
    private const int MaxIdsPerRequest = 500;

    private const string LinesQuery = "query($ids:[ID]){ lines(ids:$ids){ id publicCode name transportMode } }";
    private const string StopsQuery = "query($ids:[String]){ quays(ids:$ids){ id name latitude longitude } }";
    private const string TripQuery = """
        query($id:String!){ serviceJourney(id:$id){
            id
            line { id publicCode name transportMode }
            pointsOnLink { length points }
            quays { id name latitude longitude }
            passingTimes { quay { id } arrival { time } departure { time } }
        } }
        """;

    public async Task<IReadOnlyList<TransitLine>> GetLinesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var lines = new List<TransitLine>();
        foreach (var chunk in ids.Distinct().Chunk(MaxIdsPerRequest))
        {
            var data = await PostAsync(LinesQuery, new { ids = chunk }, cancellationToken);
            foreach (var item in data.GetProperty("lines").EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null) continue; // unknown id
                lines.Add(ReadLine(item));
            }
        }
        return lines;
    }

    public async Task<IReadOnlyList<TransitStop>> GetStopsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var stops = new List<TransitStop>();
        foreach (var chunk in ids.Distinct().Chunk(MaxIdsPerRequest))
        {
            var data = await PostAsync(StopsQuery, new { ids = chunk }, cancellationToken);
            foreach (var item in data.GetProperty("quays").EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null) continue; // unknown id
                stops.Add(ReadStop(item));
            }
        }
        return stops;
    }

    public async Task<TripRoute?> GetTripRouteAsync(string tripId, CancellationToken cancellationToken)
    {
        var data = await PostAsync(TripQuery, new { id = tripId }, cancellationToken);
        var journey = data.GetProperty("serviceJourney");
        if (journey.ValueKind == JsonValueKind.Null) return null;

        var line = journey.GetProperty("line") is { ValueKind: JsonValueKind.Object } lineJson ? ReadLine(lineJson) : null;
        var stops = journey.GetProperty("quays").EnumerateArray().Select(ReadStop).ToList();

        var stopTimes = ReadStopTimes(journey, stops);

        var path = journey.GetProperty("pointsOnLink") is { ValueKind: JsonValueKind.Object } link
            && link.GetProperty("points").GetString() is { Length: > 0 } encoded
                ? PolylineDecoder.Decode(encoded)
                : [];

        // Some operators publish a "path" that is just their stops in order (Flytoget: 9 points for
        // 9 stops over 82 km). It draws straight lines between stations, not the track, so it is
        // flagged like a missing path: clients must not present it, or snap positions to it, as the real route.
        var onlyStops = path.Count > 0 && path.Count <= stops.Count;
        if (path.Count > 0) return new TripRoute(tripId, line, path, stops, PathIsApproximate: onlyStops, stopTimes);
        var stopPath = stops.Select(stop => new GeoPoint(stop.Latitude, stop.Longitude)).ToList();
        return new TripRoute(tripId, line, stopPath, stops, PathIsApproximate: true, stopTimes);
    }

    /// <summary>
    /// Timetable per stop. passingTimes comes in the same order as quays, as local "HH:mm:ss"; the
    /// quay ids are compared anyway, and if the two lists do not line up no times are offered at all.
    /// </summary>
    private static IReadOnlyList<PlannedStopTime>? ReadStopTimes(JsonElement journey, List<TransitStop> stops)
    {
        if (!journey.TryGetProperty("passingTimes", out var passing) || passing.ValueKind != JsonValueKind.Array) return null;
        var items = passing.EnumerateArray().ToList();
        if (items.Count != stops.Count) return null;

        var times = new List<PlannedStopTime>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var id = item.TryGetProperty("quay", out var quay) && quay.ValueKind == JsonValueKind.Object
                ? ReadOptionalString(quay, "id")
                : null;
            if (id != stops[i].Id) return null;
            times.Add(new PlannedStopTime(id, ReadTime(item, "arrival"), ReadTime(item, "departure")));
        }
        return times;
    }

    private static TimeOnly? ReadTime(JsonElement json, string name) =>
        json.TryGetProperty(name, out var part) && part.ValueKind == JsonValueKind.Object
            && ReadOptionalString(part, "time") is { } text
            && TimeOnly.TryParseExact(text, "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)
                ? time
                : null;

    private static TransitLine ReadLine(JsonElement json) => new(
        json.GetProperty("id").GetString()!,
        ReadOptionalString(json, "publicCode"),
        ReadOptionalString(json, "name"),
        ReadOptionalString(json, "transportMode"));

    private static TransitStop ReadStop(JsonElement json) => new(
        json.GetProperty("id").GetString()!,
        ReadOptionalString(json, "name") ?? string.Empty,
        json.GetProperty("latitude").GetDouble(),
        json.GetProperty("longitude").GetDouble());

    private static string? ReadOptionalString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Sends one GraphQL request and returns its "data" part.</summary>
    private async Task<JsonElement> PostAsync(string query, object variables, CancellationToken cancellationToken)
    {
        // HttpClient.Timeout stops at the response headers when the body is read separately. A connection
        // that dies mid-body (network drop, laptop sleep) would then hang the poll loop forever, silently.
        // This deadline covers the whole exchange, body included.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(http.Timeout);
        try
        {
            using var response = await http.PostAsJsonAsync(options.Value.Url, new { query, variables }, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new TransitCatalogException($"The journey planner answered with HTTP {(int)response.StatusCode}.");

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(deadline.Token), cancellationToken: deadline.Token);

            // GraphQL reports problems inside a 200 response.
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var message = errors[0].TryGetProperty("message", out var text) ? text.GetString() : "unknown error";
                throw new TransitCatalogException($"The journey planner reported an error: {message}");
            }

            // Cloned because the document is disposed when this method returns.
            return document.RootElement.GetProperty("data").Clone();
        }
        catch (HttpRequestException ex)
        {
            throw new TransitCatalogException($"The journey planner could not be reached: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own token is not cancelled, so this is the deadline (or HttpClient timeout).
            throw new TransitCatalogException("The journey planner request timed out.", ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new TransitCatalogException($"The journey planner answer could not be understood: {ex.Message}", ex);
        }
    }
}
