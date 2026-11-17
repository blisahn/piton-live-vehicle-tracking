using System.Collections.Concurrent;
using System.Threading.Channels;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Application.Transit;

/// <summary>
/// Line and stop names for every id the live feed mentions, kept in memory.
///
/// It listens to live changes like any other publisher: an id it has not seen goes into a queue,
/// and a background worker resolves the queue in batches. Collecting positions never waits for
/// a name; until one arrives, clients simply show the id.
/// </summary>
public sealed class ReferenceDataService(ITransitCatalog catalog, IReferenceDataStore store, TimeProvider time) : ILivePublisher
{
    /// <summary>Well under what the catalog accepts in one request, and enough for a full feed snapshot.</summary>
    public const int MaxBatchSize = 500;

    private readonly ConcurrentDictionary<string, TransitLine> _lines = new();
    private readonly ConcurrentDictionary<string, TransitStop> _stops = new();

    // Ids already queued or found unknown, so the same id is never queued twice.
    private readonly ConcurrentDictionary<string, byte> _seenLineIds = new();
    private readonly ConcurrentDictionary<string, byte> _seenStopIds = new();

    // Bounded so a flood of new ids can never use unbounded memory; an id that does not fit is
    // simply not marked as seen and gets queued again the next time the feed mentions it.
    private readonly Channel<string> _pendingLines = Channel.CreateBounded<string>(new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Channel<string> _pendingStops = Channel.CreateBounded<string>(new BoundedChannelOptions(50_000) { FullMode = BoundedChannelFullMode.DropWrite });

    public IReadOnlyList<TransitLine> Lines => _lines.Values.ToList();

    public int KnownStopCount => _stops.Count;

    public TransitLine? FindLine(string id) => _lines.GetValueOrDefault(id);

    public TransitStop? FindStop(string id) => _stops.GetValueOrDefault(id);

    /// <summary>Loads what earlier runs already looked up, and queues entries that are due for a refresh.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var stored = await store.LoadAllAsync(cancellationToken);
        foreach (var line in stored.Lines) _lines[line.Id] = line;
        foreach (var stop in stored.Stops) _stops[stop.Id] = stop;
        foreach (var id in stored.StaleLineIds) _pendingLines.Writer.TryWrite(id);
        foreach (var id in stored.StaleStopIds) _pendingStops.Writer.TryWrite(id);
    }

    /// <summary>Called on the polling thread for every live change; only queues, never waits.</summary>
    public void Publish(LiveChange change)
    {
        foreach (var position in change.Upserted)
        {
            if (position.RouteId is { } lineId) Queue(lineId, _lines, _seenLineIds, _pendingLines);
            if (position.StopId is { } stopId) Queue(stopId, _stops, _seenStopIds, _pendingStops);
        }
    }

    /// <summary>Asks the catalog for one batch of queued ids and stores the answers.</summary>
    /// <returns>How many ids were resolved; 0 when nothing was queued.</returns>
    /// <exception cref="TransitCatalogException">The catalog could not be reached; the batch is queued again.</exception>
    public async Task<int> ResolvePendingAsync(CancellationToken cancellationToken)
    {
        var lineIds = Drain(_pendingLines);
        var stopIds = Drain(_pendingStops);
        if (lineIds.Count == 0 && stopIds.Count == 0) return 0;

        IReadOnlyList<TransitLine> lines;
        IReadOnlyList<TransitStop> stops;
        try
        {
            lines = lineIds.Count > 0 ? await catalog.GetLinesAsync(lineIds, cancellationToken) : [];
            stops = stopIds.Count > 0 ? await catalog.GetStopsAsync(stopIds, cancellationToken) : [];
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Nothing was learned: forget that these ids were seen, so the feed queues them again.
            foreach (var id in lineIds) _seenLineIds.TryRemove(id, out _);
            foreach (var id in stopIds) _seenStopIds.TryRemove(id, out _);
            throw;
        }

        // In memory first: clients can use the names even if saving them fails (the database may be down).
        Remember(lines, stops);
        await store.SaveAsync(lines, stops, time.GetUtcNow(), cancellationToken);
        // Ids the catalog does not know stay marked as seen, so they are not asked for again and again.
        return lineIds.Count + stopIds.Count;
    }

    /// <summary>Adds entries learned elsewhere (for example the stops of a trip route).</summary>
    public void Remember(IEnumerable<TransitLine> lines, IEnumerable<TransitStop> stops)
    {
        foreach (var line in lines) { _lines[line.Id] = line; _seenLineIds.TryAdd(line.Id, 0); }
        foreach (var stop in stops) { _stops[stop.Id] = stop; _seenStopIds.TryAdd(stop.Id, 0); }
    }

    /// <summary>Known stops for the given ids; unknown ones are looked up first, in one request.</summary>
    /// <exception cref="TransitCatalogException">Some ids were unknown and the catalog could not be reached.</exception>
    public async Task<IReadOnlyList<TransitStop>> GetStopsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var missing = ids.Where(id => !_stops.ContainsKey(id)).Distinct().ToList();
        if (missing.Count > 0)
        {
            // Kept in memory only, so a client request never depends on the database being up.
            // After a restart such a stop is simply looked up again when someone needs it.
            Remember([], await catalog.GetStopsAsync(missing, cancellationToken));
        }
        return ids.Select(FindStop).OfType<TransitStop>().ToList();
    }

    private static void Queue<T>(string id, ConcurrentDictionary<string, T> known, ConcurrentDictionary<string, byte> seen, Channel<string> pending)
    {
        if (known.ContainsKey(id) || !seen.TryAdd(id, 0)) return;
        if (!pending.Writer.TryWrite(id)) seen.TryRemove(id, out _);
    }

    private static List<string> Drain(Channel<string> pending)
    {
        var batch = new List<string>();
        while (batch.Count < MaxBatchSize && pending.Reader.TryRead(out var id)) batch.Add(id);
        return batch;
    }
}
