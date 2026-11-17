using VehicleTracking.Infrastructure.Entur;
using Rt = TransitRealtime;

namespace VehicleTracking.UnitTests;

public class TripUpdateFeedMappingTests
{
    private static Rt.TripUpdate.Types.StopTimeUpdate Stu(
        uint sequence, string stopId, int? arrival = null, int? departure = null,
        Rt.TripUpdate.Types.StopTimeUpdate.Types.ScheduleRelationship? relationship = null)
    {
        var stu = new Rt.TripUpdate.Types.StopTimeUpdate { StopSequence = sequence, StopId = stopId };
        if (arrival is { } a) stu.Arrival = new Rt.TripUpdate.Types.StopTimeEvent { Delay = a };
        if (departure is { } d) stu.Departure = new Rt.TripUpdate.Types.StopTimeEvent { Delay = d };
        if (relationship is { } r) stu.ScheduleRelationship = r;
        return stu;
    }

    private static Rt.FeedMessage Feed(string tripId, Rt.TripDescriptor.Types.ScheduleRelationship? trip, params Rt.TripUpdate.Types.StopTimeUpdate[] stops)
    {
        var update = new Rt.TripUpdate { Trip = new Rt.TripDescriptor { TripId = tripId, StartDate = "20261117", RouteId = "RUT:Line:1" } };
        if (trip is { } t) update.Trip.ScheduleRelationship = t;
        update.StopTimeUpdate.AddRange(stops);
        var feed = new Rt.FeedMessage { Header = new Rt.FeedHeader { GtfsRealtimeVersion = "2.0", Timestamp = 1_700_000_000 } };
        feed.Entity.Add(new Rt.FeedEntity { Id = tripId, TripUpdate = update });
        return feed;
    }

    [Fact]
    public void Delay_only_events_are_mapped_with_trip_details()
    {
        var snapshot = EnturTripUpdateFeedSource.Map(Feed("RUT:ServiceJourney:1", null, Stu(1, "NSR:Quay:1", departure: 60), Stu(2, "NSR:Quay:2", 120, 130)));

        var trip = Assert.Single(snapshot.Trips);
        Assert.Equal(new DateOnly(2026, 11, 17), trip.StartDate);
        Assert.Equal("RUT:Line:1", trip.RouteId);
        Assert.False(trip.Canceled);
        Assert.Null(trip.Stops[0].ArrivalDelay);
        Assert.Equal(60, trip.Stops[0].DepartureDelay);
        Assert.Equal(120, trip.Stops[1].ArrivalDelay);
        Assert.Equal(2, trip.Stops[1].Sequence);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), snapshot.GeneratedAt);
    }

    [Fact]
    public void Garbage_delays_are_dropped_and_counted()
    {
        var snapshot = EnturTripUpdateFeedSource.Map(Feed("t", null,
            Stu(1, "a", arrival: -1683114601, departure: 198885716),
            Stu(2, "b", arrival: 6 * 3600, departure: -6 * 3600 - 1)));

        var stops = snapshot.Trips[0].Stops;
        Assert.Null(stops[0].ArrivalDelay);
        Assert.Null(stops[0].DepartureDelay);
        Assert.Equal(6 * 3600, stops[1].ArrivalDelay); // exactly at the limit is still believed
        Assert.Null(stops[1].DepartureDelay);
        Assert.Equal(3, snapshot.DroppedDelays);
    }

    [Fact]
    public void Skipped_stops_and_canceled_trips_are_flagged()
    {
        var snapshot = EnturTripUpdateFeedSource.Map(Feed("t", Rt.TripDescriptor.Types.ScheduleRelationship.Canceled,
            Stu(1, "a"), Stu(2, "b", relationship: Rt.TripUpdate.Types.StopTimeUpdate.Types.ScheduleRelationship.Skipped)));

        Assert.True(snapshot.Trips[0].Canceled);
        Assert.False(snapshot.Trips[0].Stops[0].Skipped);
        Assert.True(snapshot.Trips[0].Stops[1].Skipped);
    }

    [Fact]
    public void Equal_stop_ids_share_one_string_instance()
    {
        var feed = Feed("t1", null, Stu(1, new string("NSR:Quay:1".ToCharArray())));
        var second = Feed("t2", null, Stu(1, new string("NSR:Quay:1".ToCharArray())));
        feed.Entity.Add(second.Entity[0]);

        var snapshot = EnturTripUpdateFeedSource.Map(feed);

        Assert.Same(snapshot.Trips[0].Stops[0].StopId, snapshot.Trips[1].Stops[0].StopId);
    }
}
