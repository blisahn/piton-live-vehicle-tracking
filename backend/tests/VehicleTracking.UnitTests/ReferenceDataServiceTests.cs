using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;
using VehicleTracking.Application.Transit;
using VehicleTracking.Domain.Vehicles;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class ReferenceDataServiceTests
{
    private readonly ITransitCatalog _catalog = Substitute.For<ITransitCatalog>();
    private readonly IReferenceDataStore _store = Substitute.For<IReferenceDataStore>();
    private readonly ReferenceDataService _service;

    public ReferenceDataServiceTests()
    {
        _catalog.GetLinesAsync(default!, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<TransitLine>>([]));
        _catalog.GetStopsAsync(default!, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<TransitStop>>([]));
        _service = new ReferenceDataService(_catalog, _store, new FakeTimeProvider(Now));
    }

    private static VehiclePosition PositionOn(string? lineId, string? stopId) =>
        new("RUT:1", Now, 59.9, 10.7, null, null, lineId, null, stopId, null, null);

    private void FeedMentions(params VehiclePosition[] positions) =>
        _service.Publish(new LiveChange(1, positions, []));

    private void CatalogKnowsLine(string id, string name) =>
        _catalog.GetLinesAsync(default!, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<TransitLine>>([new TransitLine(id, "1", name, "bus")]));

    private void CatalogKnowsStop(string id, string name) =>
        _catalog.GetStopsAsync(default!, default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<TransitStop>>([new TransitStop(id, name, 59.9, 10.7)]));

    [Fact]
    public async Task An_unknown_id_is_resolved_by_the_next_resolve_and_then_found()
    {
        CatalogKnowsLine("RUT:Line:1", "Majorstuen");
        CatalogKnowsStop("NSR:Quay:1", "Jernbanetorget");
        FeedMentions(PositionOn("RUT:Line:1", "NSR:Quay:1"));
        Assert.Null(_service.FindLine("RUT:Line:1"));

        var resolved = await _service.ResolvePendingAsync(CancellationToken.None);

        Assert.Equal(2, resolved);
        Assert.Equal("Majorstuen", _service.FindLine("RUT:Line:1")?.Name);
        Assert.Equal("Jernbanetorget", _service.FindStop("NSR:Quay:1")?.Name);
    }

    [Fact]
    public async Task The_same_id_seen_many_times_is_requested_once()
    {
        for (var i = 0; i < 5; i++) FeedMentions(PositionOn("RUT:Line:1", null));

        await _service.ResolvePendingAsync(CancellationToken.None);

        await _catalog.Received(1).GetLinesAsync(
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "RUT:Line:1" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_already_known_id_is_never_requested()
    {
        _service.Remember([new TransitLine("RUT:Line:1", "1", "Known", "bus")], []);
        FeedMentions(PositionOn("RUT:Line:1", null));

        var resolved = await _service.ResolvePendingAsync(CancellationToken.None);

        Assert.Equal(0, resolved);
        await _catalog.DidNotReceiveWithAnyArgs().GetLinesAsync(default!, default);
    }

    [Fact]
    public async Task When_the_catalog_throws_the_ids_are_requested_again_after_the_feed_mentions_them_again()
    {
        _catalog.GetLinesAsync(default!, default).ThrowsAsyncForAnyArgs(new TransitCatalogException("down"));
        FeedMentions(PositionOn("RUT:Line:1", null));

        await Assert.ThrowsAsync<TransitCatalogException>(() => _service.ResolvePendingAsync(CancellationToken.None));

        FeedMentions(PositionOn("RUT:Line:1", null));
        await Assert.ThrowsAsync<TransitCatalogException>(() => _service.ResolvePendingAsync(CancellationToken.None));
        await _catalog.ReceivedWithAnyArgs(2).GetLinesAsync(default!, default);
    }

    [Fact]
    public async Task An_id_the_catalog_does_not_know_is_not_requested_again()
    {
        FeedMentions(PositionOn("RUT:Line:404", null));
        await _service.ResolvePendingAsync(CancellationToken.None);

        FeedMentions(PositionOn("RUT:Line:404", null));
        var resolved = await _service.ResolvePendingAsync(CancellationToken.None);

        Assert.Equal(0, resolved);
        await _catalog.ReceivedWithAnyArgs(1).GetLinesAsync(default!, default);
    }

    [Fact]
    public async Task When_saving_fails_the_names_are_still_available_in_memory()
    {
        CatalogKnowsLine("RUT:Line:1", "Majorstuen");
        _store.SaveAsync(default!, default!, default, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("db down"));
        FeedMentions(PositionOn("RUT:Line:1", null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ResolvePendingAsync(CancellationToken.None));

        Assert.Equal("Majorstuen", _service.FindLine("RUT:Line:1")?.Name);
    }

    [Fact]
    public async Task Load_makes_stored_names_available_and_queues_the_stale_ids()
    {
        var stored = new StoredReferenceData(
            [new TransitLine("RUT:Line:1", "1", "Old name", "bus")],
            [new TransitStop("NSR:Quay:1", "Old stop", 59.9, 10.7)],
            StaleLineIds: ["RUT:Line:1"],
            StaleStopIds: []);
        _store.LoadAllAsync(Arg.Any<CancellationToken>()).Returns(stored);

        await _service.LoadAsync(CancellationToken.None);

        Assert.Equal("Old name", _service.FindLine("RUT:Line:1")?.Name);
        Assert.Equal("Old stop", _service.FindStop("NSR:Quay:1")?.Name);

        await _service.ResolvePendingAsync(CancellationToken.None);
        await _catalog.Received(1).GetLinesAsync(
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "RUT:Line:1" })), Arg.Any<CancellationToken>());
        await _catalog.DidNotReceiveWithAnyArgs().GetStopsAsync(default!, default);
    }

    [Fact]
    public async Task At_most_MaxBatchSize_ids_are_requested_per_call()
    {
        var positions = Enumerable.Range(0, ReferenceDataService.MaxBatchSize + 100)
            .Select(i => PositionOn($"RUT:Line:{i}", null)).ToArray();
        FeedMentions(positions);

        var first = await _service.ResolvePendingAsync(CancellationToken.None);
        var second = await _service.ResolvePendingAsync(CancellationToken.None);

        Assert.Equal(ReferenceDataService.MaxBatchSize, first);
        Assert.Equal(100, second);
    }

    [Fact]
    public async Task GetStopsAsync_looks_up_only_the_missing_ids()
    {
        _service.Remember([], [new TransitStop("NSR:Quay:1", "Known", 59.9, 10.7)]);
        CatalogKnowsStop("NSR:Quay:2", "Fetched");

        var stops = await _service.GetStopsAsync(["NSR:Quay:1", "NSR:Quay:2"], CancellationToken.None);

        Assert.Equal(["Known", "Fetched"], stops.Select(s => s.Name));
        await _catalog.Received(1).GetStopsAsync(
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "NSR:Quay:2" })), Arg.Any<CancellationToken>());
    }
}
