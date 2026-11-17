using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Status;
using VehicleTracking.Application.Transit;
using VehicleTracking.Application.TripUpdates;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class TripProgressServiceTests
{
    private const string TripId = "RUT:ServiceJourney:1";

    private readonly ITransitCatalog _catalog = Substitute.For<ITransitCatalog>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly TripUpdateService _updates;
    private readonly TripProgressService _service;

    public TripProgressServiceTests()
    {
        _updates = new TripUpdateService(_time);
        var referenceData = new ReferenceDataService(_catalog, Substitute.For<IReferenceDataStore>(), _time);
        _service = new TripProgressService(_updates, new TripRouteService(_catalog, referenceData, _time));
    }

    private static TimeOnly T(int h, int m) => new(h, m);

    private static TripRoute Route(params (string Id, TimeOnly? Arr, TimeOnly? Dep)[] stops) => new(
        TripId, null, [],
        stops.Select(s => new TransitStop(s.Id, "Name " + s.Id, 59.9, 10.7)).ToList(),
        PathIsApproximate: true,
        stops.Select(s => new PlannedStopTime(s.Id, s.Arr, s.Dep)).ToList());

    private void RouteIs(TripRoute? route) =>
        _catalog.GetTripRouteAsync(TripId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(route));

    private void Feed(DateOnly? date, params StopDelay[] stops) =>
        _updates.Apply(new TripUpdateSnapshot(Now, [new TripProgressRecord(TripId, date, "RUT:Line:1", false, Now, stops)], 0));

    private static StopDelay Stop(int seq, string id, int? arr = null, int? dep = null, bool skipped = false) =>
        new(seq, id, arr, dep, skipped);

    private static readonly DateOnly Winter = new(2026, 11, 17); // Oslo is UTC+1
    private static readonly DateOnly Summer = new(2026, 7, 1); // Oslo is UTC+2

    [Fact]
    public async Task A_trip_missing_from_the_feed_gives_null()
    {
        Assert.Null(await _service.GetAsync("unknown", Now, CancellationToken.None));
    }

    [Fact]
    public async Task Expected_times_are_the_aimed_oslo_time_plus_the_delay()
    {
        RouteIs(Route(("a", null, T(8, 0)), ("b", T(8, 10), T(8, 11))));
        Feed(Winter, Stop(1, "a", dep: 240), Stop(2, "b", arr: 240, dep: 300));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.NotNull(view);
        var a = view.Stops[0];
        Assert.Equal("Name a", a.Name);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(1)), a.AimedDeparture);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 4, 0, TimeSpan.FromHours(1)), a.ExpectedDeparture);
        Assert.Null(a.AimedArrival);
        var b = view.Stops[1];
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 14, 0, TimeSpan.FromHours(1)), b.ExpectedArrival);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 16, 0, TimeSpan.FromHours(1)), b.ExpectedDeparture);
    }

    [Fact]
    public async Task Summer_dates_use_the_summer_offset()
    {
        RouteIs(Route(("a", null, T(8, 0))));
        Feed(Summer, Stop(1, "a", dep: 0));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(2), view!.Stops[0].AimedDeparture!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 6, 0, 0, TimeSpan.Zero), view.Stops[0].AimedDeparture!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task A_trip_crossing_midnight_rolls_the_following_stops_to_the_next_day()
    {
        RouteIs(Route(("a", null, T(23, 50)), ("b", T(0, 10), T(0, 12))));
        Feed(Winter, Stop(1, "a", dep: 0), Stop(2, "b", arr: 0, dep: 0));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 11, 17, 23, 50, 0, TimeSpan.FromHours(1)), view!.Stops[0].AimedDeparture);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 0, 10, 0, TimeSpan.FromHours(1)), view.Stops[1].AimedArrival);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 0, 12, 0, TimeSpan.FromHours(1)), view.Stops[1].AimedDeparture);
    }

    [Fact]
    public async Task A_service_day_spanning_the_autumn_clock_change_gets_the_right_offsets()
    {
        // 2026-10-25: Oslo goes back from UTC+2 to UTC+1 at 03:00 local.
        RouteIs(Route(("a", null, T(23, 0)), ("b", T(4, 0), T(4, 0))));
        Feed(new DateOnly(2026, 10, 24), Stop(1, "a", dep: 0), Stop(2, "b", arr: 0, dep: 0));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(2), view!.Stops[0].AimedDeparture!.Value.Offset);
        Assert.Equal(TimeSpan.FromHours(1), view.Stops[1].AimedArrival!.Value.Offset);
    }

    [Fact]
    public async Task A_stop_without_its_own_delay_inherits_the_last_known_one()
    {
        RouteIs(Route(("a", null, T(8, 0)), ("b", T(8, 5), T(8, 5)), ("c", T(8, 10), null)));
        Feed(Winter, Stop(1, "a", dep: 120), Stop(2, "b"), Stop(3, "c"));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(120, view!.Stops[1].ArrivalDelaySeconds);
        Assert.Equal(120, view.Stops[2].ArrivalDelaySeconds);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 12, 0, TimeSpan.FromHours(1)), view.Stops[2].ExpectedArrival);
    }

    [Fact]
    public async Task A_stop_with_only_a_departure_delay_uses_it_for_its_arrival()
    {
        // Seen live on the first stop of a trip: Entur's own estimate used the departure delay there.
        RouteIs(Route(("a", T(8, 0), T(8, 0)), ("b", T(8, 5), T(8, 5))));
        Feed(Winter, Stop(1, "a", dep: -40), Stop(2, "b", arr: 260));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(-40, view!.Stops[0].ArrivalDelaySeconds);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 7, 59, 20, TimeSpan.FromHours(1)), view.Stops[0].ExpectedArrival);
    }

    [Fact]
    public async Task Departure_delay_is_what_later_stops_inherit()
    {
        RouteIs(Route(("a", T(8, 0), T(8, 1)), ("b", T(8, 5), T(8, 5))));
        Feed(Winter, Stop(1, "a", arr: 60, dep: 30), Stop(2, "b"));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal(30, view!.Stops[1].ArrivalDelaySeconds);
    }

    [Fact]
    public async Task Feed_stops_are_matched_by_sequence_when_ids_agree_and_by_id_otherwise()
    {
        RouteIs(Route(("a", null, T(8, 0)), ("b", T(8, 5), T(8, 6)), ("c", T(8, 10), null)));
        // Sequence 1 points at "a" (agrees); sequence 1 for "c" disagrees, so "c" is found by id;
        // "zzz" is unknown to the route but is still returned.
        Feed(Winter, Stop(1, "a", dep: 0), Stop(1, "c", arr: 60), Stop(9, "zzz", arr: 0));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Equal("Name a", view!.Stops[0].Name);
        Assert.Equal("Name c", view.Stops[1].Name);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 8, 11, 0, TimeSpan.FromHours(1)), view.Stops[1].ExpectedArrival);
        Assert.Equal("zzz", view.Stops[2].StopId);
        Assert.Null(view.Stops[2].Name);
        Assert.Null(view.Stops[2].AimedArrival);
    }

    [Fact]
    public async Task The_trip_delay_is_the_one_at_the_next_upcoming_stop()
    {
        RouteIs(Route(("a", null, T(8, 0)), ("b", T(8, 10), T(8, 10)), ("c", T(8, 20), null)));
        Feed(Winter, Stop(1, "a", dep: 60), Stop(2, "b", arr: 180, dep: 180), Stop(3, "c", arr: 300));
        var at = (DateTimeOffset t) => _service.GetAsync(TripId, t, CancellationToken.None);
        var eight = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(60, (await at(eight.AddMinutes(-5)))!.DelaySeconds);   // before the first stop
        Assert.Equal(180, (await at(eight.AddMinutes(5)))!.DelaySeconds);   // between a and b
        Assert.Equal(300, (await at(eight.AddMinutes(30)))!.DelaySeconds);  // all passed: the last stop
    }

    [Fact]
    public async Task No_delays_at_all_gives_a_null_trip_delay()
    {
        RouteIs(Route(("a", null, T(8, 0))));
        Feed(Winter, Stop(1, "a"));

        Assert.Null((await _service.GetAsync(TripId, Now, CancellationToken.None))!.DelaySeconds);
    }

    [Fact]
    public async Task A_failing_route_lookup_still_returns_the_delays_without_times()
    {
        _catalog.GetTripRouteAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new TransitCatalogException("down"));
        Feed(Winter, Stop(1, "a", arr: 120));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        var stop = Assert.Single(view!.Stops);
        Assert.Equal(120, stop.ArrivalDelaySeconds);
        Assert.Null(stop.Name);
        Assert.Null(stop.AimedArrival);
        Assert.Null(stop.ExpectedArrival);
        Assert.Equal(120, view.DelaySeconds);
    }

    [Fact]
    public async Task An_unknown_route_returns_the_delays_without_times()
    {
        RouteIs(null);
        Feed(Winter, Stop(1, "a", dep: 60));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Null(view!.Stops[0].ExpectedDeparture);
        Assert.Equal(60, view.DelaySeconds);
    }

    [Fact]
    public async Task Without_a_start_date_there_are_no_absolute_times()
    {
        RouteIs(Route(("a", null, T(8, 0))));
        Feed(null, Stop(1, "a", dep: 60));

        var view = await _service.GetAsync(TripId, Now, CancellationToken.None);

        Assert.Null(view!.Stops[0].AimedDeparture);
        Assert.Equal("Name a", view.Stops[0].Name);
    }
}

public class TripUpdateServiceTests
{
    private readonly FakeTimeProvider _time = new(Now);
    private readonly TripUpdateService _service;

    public TripUpdateServiceTests() => _service = new TripUpdateService(_time);

    private static TripProgressRecord Trip(string id) => new(id, null, null, false, null, []);

    [Fact]
    public void Apply_replaces_the_trips_and_updates_the_status()
    {
        _service.Apply(new TripUpdateSnapshot(Now, [Trip("a"), Trip("b")], 2));
        _service.Apply(new TripUpdateSnapshot(Now, [Trip("b")], 1));

        Assert.Null(_service.TryGet("a"));
        Assert.NotNull(_service.TryGet("b"));
        var status = _service.Status;
        Assert.Equal(Now, status.LastSuccessfulReadAt);
        Assert.Equal(2, status.SuccessfulReads);
        Assert.Equal(1, status.TripCount);
        Assert.Equal(3, status.DroppedDelays);
    }

    [Fact]
    public void A_failure_keeps_the_previous_trips_and_is_counted()
    {
        _service.Apply(new TripUpdateSnapshot(Now, [Trip("a")], 0));
        _service.RecordFailure("boom");

        Assert.NotNull(_service.TryGet("a"));
        Assert.Equal("boom", _service.Status.LastError);
        Assert.Equal(1, _service.Status.FailedReads);

        _service.Apply(new TripUpdateSnapshot(Now, [Trip("a")], 0));
        Assert.Null(_service.Status.LastError);
    }
}
