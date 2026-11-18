using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.History;

namespace VehicleTracking.IntegrationTests;

/// <summary>Keyset paging through one vehicle's history against the real index and query.</summary>
public sealed class HistoryPagingTests(DatabaseFixture db) : DatabaseTest(db)
{
    private const string Vehicle = "ATB:2667";

    private async Task<List<HistoryPage>> ReadAllPagesAsync(HistoryQuery first)
    {
        using var scope = Db.Services.CreateScope();
        var history = scope.ServiceProvider.GetRequiredService<VehicleHistoryService>();
        var pages = new List<HistoryPage>();
        var query = first;
        while (true)
        {
            var page = await history.GetPageAsync(query, default);
            pages.Add(page);
            if (page.NextCursor is null) return pages;
            query = query with { Cursor = page.NextCursor };
        }
    }

    [DockerFact]
    public async Task Pages_cover_every_position_once_newest_first_and_skip_other_vehicles()
    {
        var now = Positions.Now();
        var track = Positions.Track(Vehicle, now, 250);
        var writer = Db.Services.GetRequiredService<IPositionWriter>();
        await writer.WriteAsync(track, default);
        await writer.WriteAsync(Positions.Track("KOL:2667", now, 50), default);

        var pages = await ReadAllPagesAsync(new HistoryQuery(Vehicle, null, null, null, PageSize: 100));

        Assert.Equal([100, 100, 50], pages.Select(p => p.Items.Count));
        var all = pages.SelectMany(p => p.Items).ToList();
        Assert.All(all, p => Assert.Equal(Vehicle, p.VehicleKey));
        Assert.Equal(track.Select(p => p.RecordedAt).Reverse(), all.Select(p => p.RecordedAt));
    }

    [DockerFact]
    public async Task Positions_written_while_paging_do_not_shift_the_later_pages()
    {
        var now = Positions.Now();
        var writer = Db.Services.GetRequiredService<IPositionWriter>();
        await writer.WriteAsync(Positions.Track(Vehicle, now.AddSeconds(-10), 30), default);

        using var scope = Db.Services.CreateScope();
        var history = scope.ServiceProvider.GetRequiredService<VehicleHistoryService>();
        var query = new HistoryQuery(Vehicle, null, now.AddSeconds(5), null, PageSize: 10);
        var first = await history.GetPageAsync(query, default);

        // New positions arrive at the top while the user is on page one.
        await writer.WriteAsync(Positions.Track(Vehicle, now, 5), default);
        var second = await history.GetPageAsync(query with { Cursor = first.NextCursor }, default);

        Assert.True(second.Items[0].RecordedAt < first.Items[^1].RecordedAt);
        Assert.Equal(first.Items[^1].RecordedAt.AddSeconds(-1), second.Items[0].RecordedAt);
    }

    [DockerFact]
    public async Task The_time_range_is_from_inclusive_to_exclusive()
    {
        var now = Positions.Now();
        await Db.Services.GetRequiredService<IPositionWriter>().WriteAsync(Positions.Track(Vehicle, now, 60), default);
        var from = now.AddSeconds(-40);
        var to = now.AddSeconds(-20);

        var items = (await ReadAllPagesAsync(new HistoryQuery(Vehicle, from, to, null, PageSize: 7)))
            .SelectMany(p => p.Items).ToList();

        Assert.Equal(20, items.Count);
        Assert.Equal(to.AddSeconds(-1), items[0].RecordedAt);
        Assert.Equal(from, items[^1].RecordedAt);
    }

    [DockerFact]
    public async Task An_unknown_vehicle_has_an_empty_history()
    {
        var pages = await ReadAllPagesAsync(new HistoryQuery("NOPE:1", null, null, null, PageSize: 100));

        Assert.Empty(Assert.Single(pages).Items);
    }
}
