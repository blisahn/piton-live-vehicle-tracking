using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Alerts;
using VehicleTracking.Application.Transit;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class AlertServiceTests
{
    private readonly ITransitCatalog _catalog = Substitute.For<ITransitCatalog>();
    private readonly IAlertPublisher _publisher = Substitute.For<IAlertPublisher>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        var referenceData = new ReferenceDataService(_catalog, Substitute.For<IReferenceDataStore>(), _time);
        _service = new AlertService(new TripRouteService(_catalog, referenceData, _time), [_publisher], _time);
    }

    private static ServiceAlert Alert(
        string id,
        AlertPeriod[]? periods = null,
        AlertTarget[]? targets = null,
        string header = "Departure delayed") =>
        new(id, periods ?? [], targets ?? [], [new AlertText(header, null, false)], [], null);

    private static AlertFeedSnapshot Feed(params ServiceAlert[] alerts) => new(Now, alerts);

    private Task Apply(params ServiceAlert[] alerts) => _service.ApplyAsync(Feed(alerts), CancellationToken.None);

    private IReadOnlyList<string> Ids(string? line = null, string? stop = null, bool inactive = false) =>
        _service.Query(line, stop, inactive, Now).Select(v => v.Alert.Id).ToList();

    private void TripBelongsTo(string tripId, string? lineId) =>
        _catalog.GetTripRouteAsync(tripId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TripRoute?>(
            lineId is null ? null : new TripRoute(tripId, new TransitLine(lineId, "1", "One", "bus"), [], [], false)));

    [Fact]
    public async Task The_first_read_bumps_the_version_and_notifies_publishers()
    {
        await Apply(Alert("a"));

        Assert.Equal(1, _service.Version);
        _publisher.Received(1).PublishAlertsVersion(1);
    }

    [Fact]
    public async Task The_same_alerts_in_another_order_do_not_bump_the_version()
    {
        await Apply(Alert("a"), Alert("b"));
        await Apply(Alert("b"), Alert("a"));

        Assert.Equal(1, _service.Version);
        _publisher.Received(1).PublishAlertsVersion(Arg.Any<long>());
    }

    [Fact]
    public async Task A_changed_text_bumps_the_version()
    {
        await Apply(Alert("a"));
        await Apply(Alert("a", header: "Departure cancelled"));

        Assert.Equal(2, _service.Version);
        _publisher.Received(1).PublishAlertsVersion(2);
    }

    [Fact]
    public async Task Alerts_without_id_are_dropped_and_repeated_ids_keep_the_last()
    {
        await Apply(Alert(""), Alert("a", header: "first"), Alert("a", header: "second"));

        var view = Assert.Single(_service.Query(null, null, true, Now));
        Assert.Equal("second", view.Alert.Header[0].Text);
    }

    [Fact]
    public async Task Texts_without_a_tag_get_a_detected_language()
    {
        await Apply(Alert("a", header: "Avgangen er forsinket"));

        var text = _service.Query(null, null, true, Now)[0].Alert.Header[0];
        Assert.Equal("no", text.Language);
        Assert.True(text.LanguageDetected);
    }

    [Fact]
    public async Task An_alert_without_periods_is_active()
    {
        await Apply(Alert("a"));

        Assert.Equal(["a"], Ids());
    }

    [Fact]
    public async Task Alerts_outside_their_period_are_hidden_unless_inactive_ones_are_asked_for()
    {
        await Apply(
            Alert("now", [new AlertPeriod(Now.AddHours(-1), Now.AddHours(1))]),
            Alert("open-start", [new AlertPeriod(null, Now.AddHours(1))]),
            Alert("open-end", [new AlertPeriod(Now.AddHours(-1), null)]),
            Alert("past", [new AlertPeriod(Now.AddDays(-2), Now.AddDays(-1))]),
            Alert("future", [new AlertPeriod(Now.AddDays(1), null)]));

        Assert.Equal(["now", "open-end", "open-start"], Ids().OrderBy(x => x).ToList());
        Assert.Equal(["future", "past"], Ids(inactive: true).Except(Ids()).OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_is_open_ended_and_flagged()
    {
        await Apply(Alert("odd", [new AlertPeriod(Now.AddHours(-1), Now.AddHours(-2))]));

        var view = Assert.Single(_service.Query(null, null, false, Now));
        Assert.True(view.IsActive);
        Assert.True(view.HasInvalidPeriod);
    }

    [Fact]
    public async Task An_inverted_period_in_the_future_is_not_active_yet()
    {
        await Apply(Alert("odd", [new AlertPeriod(Now.AddHours(2), Now.AddHours(1))]));

        Assert.Empty(Ids());
    }

    [Fact]
    public async Task Active_alerts_come_first_then_the_newest()
    {
        await Apply(
            Alert("old", [new AlertPeriod(Now.AddDays(-3), null)]),
            Alert("past", [new AlertPeriod(Now.AddDays(-9), Now.AddDays(-8))]),
            Alert("new", [new AlertPeriod(Now.AddHours(-1), null)]));

        Assert.Equal(["new", "old", "past"], Ids(inactive: true));
    }

    [Fact]
    public async Task The_line_filter_matches_a_route_named_directly()
    {
        await Apply(
            Alert("a", targets: [new AlertTarget("L1", null, null)]),
            Alert("b", targets: [new AlertTarget("L2", null, null)]));

        Assert.Equal(["a"], Ids(line: "L1"));
    }

    [Fact]
    public async Task The_line_filter_matches_through_the_line_of_a_named_trip()
    {
        TripBelongsTo("T1", "L1");
        TripBelongsTo("T2", "L2");
        await Apply(
            Alert("a", targets: [new AlertTarget(null, "T1", null)]),
            Alert("b", targets: [new AlertTarget(null, "T2", "S1")]));

        Assert.Equal(["a"], Ids(line: "L1"));
        Assert.Equal(["L2"], _service.Query(null, null, false, Now).Single(v => v.Alert.Id == "b").LineIds);
    }

    [Fact]
    public async Task Learning_a_trips_line_bumps_the_version_again()
    {
        TripBelongsTo("T1", "L1");

        await Apply(Alert("a", targets: [new AlertTarget(null, "T1", null)]));

        Assert.Equal(2, _service.Version);
        _publisher.Received(1).PublishAlertsVersion(2);
    }

    [Fact]
    public async Task A_trip_that_could_not_be_looked_up_is_retried_next_cycle()
    {
        _catalog.GetTripRouteAsync("T1", Arg.Any<CancellationToken>()).Throws(new TransitCatalogException("down"));
        var alert = Alert("a", targets: [new AlertTarget(null, "T1", null)]);
        await Apply(alert);
        Assert.Empty(Ids(line: "L1"));

        TripBelongsTo("T1", "L1");
        await Apply(alert);

        Assert.Equal(["a"], Ids(line: "L1"));
    }

    [Fact]
    public async Task The_stop_filter_and_line_filter_are_combined()
    {
        await Apply(
            Alert("a", targets: [new AlertTarget("L1", null, "S1")]),
            Alert("b", targets: [new AlertTarget("L2", null, "S1")]),
            Alert("c", targets: [new AlertTarget(null, null, "S2")]));

        Assert.Equal(["a", "b"], Ids(stop: "S1"));
        Assert.Equal(["b"], Ids(line: "L2", stop: "S1"));
    }

    [Fact]
    public async Task Status_counts_reads_and_failures()
    {
        await Apply(Alert("a"));
        _service.RecordFailure("boom");

        var status = _service.Status;
        Assert.Equal(Now, status.LastSuccessfulReadAt);
        Assert.Equal(1, status.SuccessfulReads);
        Assert.Equal(1, status.FailedReads);
        Assert.Equal("boom", status.LastError);
        Assert.Equal(1, status.AlertCount);
    }
}
