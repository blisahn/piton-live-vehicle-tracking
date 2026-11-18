using Google.Protobuf;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Entur;

/// <summary>
/// Downloads the Entur GTFS-Realtime protobuf feed and translates it field by field.
/// No validation happens here; deciding what is usable is the SnapshotNormalizer's job.
/// </summary>
internal sealed class EnturFeedSource(HttpClient http, IOptions<FeedOptions> options) : IFeedSource
{
    public async Task<FeedSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        // HttpClient.Timeout stops at the response headers when the body is read separately. A connection
        // that dies mid-body (network drop, laptop sleep) would then hang the poll loop forever, silently.
        // This deadline covers the whole exchange, body included.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(http.Timeout);
        try
        {
            using var response = await http.GetAsync(
                options.Value.VehiclePositionsUrl, HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            if (!response.IsSuccessStatusCode)
                throw new FeedReadException($"The feed answered with HTTP {(int)response.StatusCode}.");

            // ParseFrom on a network stream would block a thread, so the bytes are read asynchronously first.
            var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token);
            var feed = TransitRealtime.FeedMessage.Parser.ParseFrom(bytes);

            return Map(feed);
        }
        catch (HttpRequestException ex)
        {
            throw new FeedReadException($"The feed could not be reached: {ex.Message}", ex);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new FeedReadException($"The feed content could not be parsed: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own token is not cancelled, so this is the deadline (or HttpClient timeout).
            throw new FeedReadException("The feed request timed out.", ex);
        }
    }

    private static FeedSnapshot Map(TransitRealtime.FeedMessage feed)
    {
        DateTimeOffset? generatedAt = feed.Header.HasTimestamp
            ? DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp)
            : null;

        var vehicles = new List<FeedVehicle>(feed.Entity.Count);
        foreach (var entity in feed.Entity)
        {
            if (entity.IsDeleted || entity.Vehicle is null) continue;

            var vp = entity.Vehicle;
            var position = vp.Position;

            vehicles.Add(new FeedVehicle(
                EntityId: entity.Id,
                VehicleId: vp.Vehicle is { HasId: true } ? vp.Vehicle.Id : null,
                Timestamp: vp.HasTimestamp ? DateTimeOffset.FromUnixTimeSeconds((long)vp.Timestamp) : null,
                Latitude: position?.Latitude,
                Longitude: position?.Longitude,
                Bearing: position is { HasBearing: true } ? position.Bearing : null,
                Speed: position is { HasSpeed: true } ? position.Speed : null,
                RouteId: vp.Trip is { HasRouteId: true } ? vp.Trip.RouteId : null,
                TripId: vp.Trip is { HasTripId: true } ? vp.Trip.TripId : null,
                StopId: vp.HasStopId ? vp.StopId : null,
                StopStatus: vp.HasCurrentStatus ? (VehicleStopStatus)(int)vp.CurrentStatus : null,
                Occupancy: vp.HasOccupancyStatus ? (OccupancyStatus)(int)vp.OccupancyStatus : null));
        }

        return new FeedSnapshot(generatedAt, vehicles);
    }
}
