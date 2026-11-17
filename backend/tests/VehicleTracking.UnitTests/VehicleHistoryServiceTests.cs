using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Application.History;
using VehicleTracking.Domain.Vehicles;
using static VehicleTracking.UnitTests.TestData;

namespace VehicleTracking.UnitTests;

public class VehicleHistoryServiceTests
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(3);
    private readonly IPositionHistoryReader _reader = Substitute.For<IPositionHistoryReader>();
    private readonly VehicleHistoryService _service;

    public VehicleHistoryServiceTests()
    {
        _service = new VehicleHistoryService(
            _reader, Options.Create(new RetentionOptions { Period = Retention }), new FakeTimeProvider(Now));
        ReaderReturns(0);
    }

    private static HistoryQuery Query(
        DateTimeOffset? from = null, DateTimeOffset? to = null, DateTimeOffset? cursor = null, int pageSize = 10) =>
        new("RUT:101", from, to, cursor, pageSize);

    private void ReaderReturns(int rowCount) =>
        _reader.ReadNewestFirstAsync(default!, default, default, default, default).ReturnsForAnyArgs(
            Enumerable.Range(0, rowCount).Select(i => Position(at: Now.AddMinutes(-i))).ToList());

    private Task<IReadOnlyList<VehiclePosition>> ReaderWasAskedFor(
        DateTimeOffset from, DateTimeOffset before, int take) =>
        _reader.Received(1).ReadNewestFirstAsync("RUT:101", from, before, take, Arg.Any<CancellationToken>());

    [Fact]
    public async Task Without_from_and_to_asks_for_the_whole_retention_window_up_to_now()
    {
        await _service.GetPageAsync(Query(), CancellationToken.None);

        await ReaderWasAskedFor(Now - Retention, Now, 11);
    }

    [Fact]
    public async Task A_from_older_than_the_retention_window_is_clamped_to_the_window_start()
    {
        await _service.GetPageAsync(Query(from: Now.AddDays(-2)), CancellationToken.None);

        await ReaderWasAskedFor(Now - Retention, Now, 11);
    }

    [Fact]
    public async Task A_from_inside_the_retention_window_is_kept()
    {
        var from = Now.AddHours(-1);

        await _service.GetPageAsync(Query(from: from), CancellationToken.None);

        await ReaderWasAskedFor(from, Now, 11);
    }

    [Fact]
    public async Task Rejects_a_from_that_is_not_earlier_than_to()
    {
        var at = Now.AddHours(-1);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.GetPageAsync(Query(from: at, to: at), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Rejects_a_page_size_outside_one_to_1000(int pageSize)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.GetPageAsync(Query(pageSize: pageSize), CancellationToken.None));
    }

    [Fact]
    public async Task When_one_extra_row_comes_back_it_returns_a_page_and_the_cursor_of_its_last_item()
    {
        ReaderReturns(11);

        var page = await _service.GetPageAsync(Query(pageSize: 10), CancellationToken.None);

        Assert.Equal(10, page.Items.Count);
        Assert.Equal(page.Items[^1].RecordedAt, page.NextCursor);
        Assert.Equal(Now.AddMinutes(-9), page.NextCursor);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(3)]
    public async Task When_no_extra_row_comes_back_there_is_no_next_cursor(int rows)
    {
        ReaderReturns(rows);

        var page = await _service.GetPageAsync(Query(pageSize: 10), CancellationToken.None);

        Assert.Equal(rows, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task A_cursor_is_passed_to_the_reader_as_the_before_bound()
    {
        var cursor = Now.AddMinutes(-30);

        await _service.GetPageAsync(Query(cursor: cursor), CancellationToken.None);

        await ReaderWasAskedFor(Now - Retention, cursor, 11);
    }
}
