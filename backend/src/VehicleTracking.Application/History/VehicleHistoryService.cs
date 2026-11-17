using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.History;

/// <summary>Pages through the stored positions of one vehicle, newest first.</summary>
public sealed class VehicleHistoryService(
    IPositionHistoryReader reader,
    IOptions<RetentionOptions> retention,
    TimeProvider time)
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 1000;

    public async Task<HistoryPage> GetPageAsync(HistoryQuery query, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var oldestKept = now - retention.Value.Period;

        var to = query.To ?? now;
        // Nothing older than the retention window exists, so an earlier start simply means "from the beginning".
        var from = query.From is { } requested && requested > oldestKept ? requested : oldestKept;

        if (from >= to)
            throw new RequestValidationException("'from' must be earlier than 'to', and 'to' must fall inside the retention window.");
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new RequestValidationException($"'limit' must be between 1 and {MaxPageSize}.");

        // Keyset pagination: the cursor is the timestamp of the last row of the previous page.
        // The index on (vehicle_key, recorded_at) jumps straight to it, so page 50 costs the same as page 1,
        // and rows arriving while the user pages cannot shift the pages the way OFFSET would.
        var before = query.Cursor is { } cursor && cursor < to ? cursor : to;

        // One extra row tells us whether another page exists without a second query.
        var rows = await reader.ReadNewestFirstAsync(query.VehicleKey, from, before, query.PageSize + 1, cancellationToken);

        var hasMore = rows.Count > query.PageSize;
        var items = hasMore ? rows.Take(query.PageSize).ToList() : rows;
        return new HistoryPage(items, hasMore ? items[^1].RecordedAt : null);
    }
}

/// <param name="From">Inclusive. Null means the start of the retention window.</param>
/// <param name="To">Exclusive. Null means now.</param>
/// <param name="Cursor"><see cref="HistoryPage.NextCursor"/> of the previous page; null for the first page.</param>
public sealed record HistoryQuery(string VehicleKey, DateTimeOffset? From, DateTimeOffset? To, DateTimeOffset? Cursor, int PageSize);

/// <param name="NextCursor">Pass as <see cref="HistoryQuery.Cursor"/> to get the next page; null on the last page.</param>
public sealed record HistoryPage(IReadOnlyList<VehiclePosition> Items, DateTimeOffset? NextCursor);
