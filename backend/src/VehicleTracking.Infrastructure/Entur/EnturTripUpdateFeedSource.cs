using Google.Protobuf;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.TripUpdates;

namespace VehicleTracking.Infrastructure.Entur;

/// <summary>
/// Downloads the Entur GTFS-Realtime trip updates (about 12 MB, ~14,000 trips, ~350,000 stop rows)
/// and boils them down to the delays.
/// </summary>
internal sealed class EnturTripUpdateFeedSource(HttpClient http, IOptions<TripUpdateOptions> options) : ITripUpdateFeedSource
{
    /// <summary>
    /// Entur's delays are sometimes garbage (-1683114601 s, 198885716 s). Nothing real is this late,
    /// and one such value would make a whole trip look broken, so it is read as "unknown".
    /// </summary>
    internal static readonly TimeSpan MaxPlausibleDelay = TimeSpan.FromHours(6);

    public async Task<TripUpdateSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        // HttpClient.Timeout stops at the response headers when the body is read separately. A connection
        // that dies mid-body (network drop, laptop sleep) would then hang the poll loop forever, silently.
        // This deadline covers the whole exchange, body included.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(http.Timeout);
        try
        {
            using var response = await http.GetAsync(
                options.Value.Url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            if (!response.IsSuccessStatusCode)
                throw new FeedReadException($"The trip updates feed answered with HTTP {(int)response.StatusCode}.");

            var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token);
            var feed = TransitRealtime.FeedMessage.Parser.ParseFrom(bytes);

            return Map(feed);
        }
        catch (HttpRequestException ex)
        {
            throw new FeedReadException($"The trip updates feed could not be reached: {ex.Message}", ex);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new FeedReadException($"The trip updates feed content could not be parsed: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FeedReadException("The trip updates feed request timed out.", ex);
        }
    }

    internal static TripUpdateSnapshot Map(TransitRealtime.FeedMessage feed)
    {
        DateTimeOffset? generatedAt = feed.Header.HasTimestamp
            ? DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp)
            : null;

        // The same ~30,000 stop ids repeat across ~350,000 rows; one string instance per id keeps them cheap.
        var stopIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var dropped = 0;
        var trips = new List<TripProgressRecord>(feed.Entity.Count);

        foreach (var entity in feed.Entity)
        {
            if (entity.IsDeleted || entity.TripUpdate is not { } update || !update.Trip.HasTripId) continue;

            var stops = new StopDelay[update.StopTimeUpdate.Count];
            for (var i = 0; i < stops.Length; i++)
            {
                var stu = update.StopTimeUpdate[i];
                var id = stu.StopId;
                if (!stopIds.TryGetValue(id, out var pooled)) stopIds[id] = pooled = id;

                stops[i] = new StopDelay(
                    (int)stu.StopSequence,
                    pooled,
                    PlausibleDelay(stu.Arrival, ref dropped),
                    PlausibleDelay(stu.Departure, ref dropped),
                    Skipped: stu.ScheduleRelationship == TransitRealtime.TripUpdate.Types.StopTimeUpdate.Types.ScheduleRelationship.Skipped);
            }

            trips.Add(new TripProgressRecord(
                update.Trip.TripId,
                ParseDate(update.Trip.HasStartDate ? update.Trip.StartDate : null),
                update.Trip.HasRouteId ? update.Trip.RouteId : null,
                Canceled: update.Trip.ScheduleRelationship == TransitRealtime.TripDescriptor.Types.ScheduleRelationship.Canceled,
                update.HasTimestamp ? DateTimeOffset.FromUnixTimeSeconds((long)update.Timestamp) : null,
                stops));
        }

        return new TripUpdateSnapshot(generatedAt, trips, dropped);
    }

    // Entur sends only "delay"; an absolute "time" is never set, so it is not read.
    private static int? PlausibleDelay(TransitRealtime.TripUpdate.Types.StopTimeEvent? e, ref int dropped)
    {
        if (e is null || !e.HasDelay) return null;
        if (Math.Abs((long)e.Delay) > MaxPlausibleDelay.TotalSeconds)
        {
            dropped++;
            return null;
        }
        return e.Delay;
    }

    private static DateOnly? ParseDate(string? yyyymmdd) =>
        DateOnly.TryParseExact(yyyymmdd, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date) ? date : null;
}
