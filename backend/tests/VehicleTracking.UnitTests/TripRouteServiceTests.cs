using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Transit;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class TripRouteServiceTests
{
    private const string TripId = "ATB:ServiceJourney:1";

    private readonly ITransitCatalog _catalog = Substitute.For<ITransitCatalog>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ReferenceDataService _referenceData;
    private readonly TripRouteService _service;

    public TripRouteServiceTests()
    {
        _referenceData = new ReferenceDataService(_catalog, Substitute.For<IReferenceDataStore>(), _time);
        _service = new TripRouteService(_catalog, _referenceData, _time);
    }

    private static TripRoute Route() => new(
        TripId,
        new TransitLine("ATB:Line:1", "1", "Line one", "bus"),
        [new GeoPoint(63.4, 10.4), new GeoPoint(63.5, 10.5)],
        [new TransitStop("NSR:Quay:1", "First", 63.4, 10.4)],
        PathIsApproximate: false);

    private void CatalogReturns(TripRoute? route) =>
        _catalog.GetTripRouteAsync(TripId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(route));

    [Fact]
    public async Task A_second_request_for_the_same_trip_does_not_call_the_catalog_again()
    {
        CatalogReturns(Route());

        await _service.GetAsync(TripId, CancellationToken.None);
        var second = await _service.GetAsync(TripId, CancellationToken.None);

        Assert.NotNull(second);
        await _catalog.Received(1).GetTripRouteAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Two_concurrent_requests_share_one_catalog_call()
    {
        var pending = new TaskCompletionSource<TripRoute?>();
        _catalog.GetTripRouteAsync(TripId, Arg.Any<CancellationToken>()).Returns(pending.Task);

        var first = _service.GetAsync(TripId, CancellationToken.None);
        var second = _service.GetAsync(TripId, CancellationToken.None);
        pending.SetResult(Route());

        Assert.NotNull(await first);
        Assert.NotNull(await second);
        await _catalog.Received(1).GetTripRouteAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_catalog_failure_is_not_cached()
    {
        _catalog.GetTripRouteAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new TransitCatalogException("down"));
        await Assert.ThrowsAsync<TransitCatalogException>(() => _service.GetAsync(TripId, CancellationToken.None));

        CatalogReturns(Route());
        var route = await _service.GetAsync(TripId, CancellationToken.None);

        Assert.NotNull(route);
        await _catalog.Received(2).GetTripRouteAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_trip_is_cached_as_null()
    {
        CatalogReturns(null);

        Assert.Null(await _service.GetAsync(TripId, CancellationToken.None));
        Assert.Null(await _service.GetAsync(TripId, CancellationToken.None));

        await _catalog.Received(1).GetTripRouteAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_entry_older_than_six_hours_is_fetched_again()
    {
        CatalogReturns(Route());
        await _service.GetAsync(TripId, CancellationToken.None);

        _time.Advance(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(1));
        await _service.GetAsync(TripId, CancellationToken.None);

        await _catalog.Received(2).GetTripRouteAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_stops_of_a_fetched_route_become_available_as_reference_data()
    {
        CatalogReturns(Route());

        await _service.GetAsync(TripId, CancellationToken.None);

        Assert.Equal("First", _referenceData.FindStop("NSR:Quay:1")?.Name);
        Assert.Equal("Line one", _referenceData.FindLine("ATB:Line:1")?.Name);
    }

    [Fact]
    public async Task Many_callers_racing_for_the_same_trip_cause_exactly_one_catalog_call()
    {
        // Without the lock around "look up, else create" this fails now and then: callers that
        // miss at the same moment each start their own request.
        var pending = new TaskCompletionSource<TripRoute?>();
        var calls = 0;
        _catalog.GetTripRouteAsync("T", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref calls);
            return pending.Task;
        });

        using var start = new ManualResetEventSlim();
        var callers = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => { start.Wait(); return _service.GetAsync("T", CancellationToken.None); }))
            .ToList();
        start.Set();
        pending.SetResult(null);
        await Task.WhenAll(callers);

        Assert.Equal(1, calls);
    }
}
