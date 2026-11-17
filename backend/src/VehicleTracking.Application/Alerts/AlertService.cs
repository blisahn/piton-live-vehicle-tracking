using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Transit;

namespace VehicleTracking.Application.Alerts;

/// <summary>
/// The current service alerts, ready to be filtered by line or stop.
///
/// One writer (the polling worker) calls <see cref="ApplyAsync"/>; any number of request threads
/// read. The alerts are an immutable value swapped in one step, so readers never take a lock
/// and never see a half-applied read. <see cref="Version"/> grows only when what a client could
/// see has changed, so clients can refetch on a version change instead of polling blindly.
/// </summary>
public sealed class AlertService(
    TripRouteService trips,
    IEnumerable<IAlertPublisher> publishers,
    TimeProvider time) : IAlertVersionReader
{
    /// <summary>Trip lookups hit the journey planner; a few at a time is polite and still fast.</summary>
    private const int MaxConcurrentLookups = 4;

    private sealed record State(long Version, DateTimeOffset? GeneratedAt, IReadOnlyList<ServiceAlert> Alerts);

    private State _state = new(0, null, []);

    // Alerts often name only a trip; this maps it to its line. Null means "looked up, it has no line".
    // A trip that failed to resolve is simply absent, so the next cycle tries it again.
    private readonly ConcurrentDictionary<string, string?> _tripLines = new();

    // Only the writer touches these.
    private string? _fingerprint;
    private AlertFeedStatus _status = new(null, null, 0, 0, 0);

    public long Version => Volatile.Read(ref _state).Version;

    public DateTimeOffset? GeneratedAt => Volatile.Read(ref _state).GeneratedAt;

    public AlertFeedStatus Status => Volatile.Read(ref _status);

    /// <summary>Takes the result of one successful feed read.</summary>
    public async Task ApplyAsync(AlertFeedSnapshot snapshot, CancellationToken cancellationToken)
    {
        var alerts = Normalize(snapshot.Alerts);
        var fingerprint = Fingerprint(alerts);

        var previous = _state;
        var contentChanged = fingerprint != _fingerprint;
        _fingerprint = fingerprint;
        Volatile.Write(ref _state, new State(
            contentChanged ? previous.Version + 1 : previous.Version, snapshot.GeneratedAt, alerts));
        var current = _status;
        Volatile.Write(ref _status, current with
        {
            LastSuccessfulReadAt = time.GetUtcNow(),
            LastError = null,
            SuccessfulReads = current.SuccessfulReads + 1,
            AlertCount = alerts.Count,
        });
        if (contentChanged) Publish(_state.Version);

        // Content is visible before the slow part, so the first read is not held up by trip lookups.
        if (await ResolveTripsAsync(alerts, cancellationToken))
        {
            var state = _state;
            Volatile.Write(ref _state, state with { Version = state.Version + 1 });
            Publish(_state.Version);
        }
    }

    public void RecordFailure(string error)
    {
        var current = _status;
        Volatile.Write(ref _status, current with { LastError = error, FailedReads = current.FailedReads + 1 });
    }

    /// <summary>Alerts matching every given filter, active ones first, then newest first.</summary>
    public IReadOnlyList<AlertView> Query(string? lineId, string? stopId, bool includeInactive, DateTimeOffset now)
    {
        var views = new List<AlertView>();
        foreach (var alert in Volatile.Read(ref _state).Alerts)
        {
            var view = ToView(alert, now);
            if (!includeInactive && !view.IsActive) continue;
            if (lineId is not null && !view.LineIds.Contains(lineId)) continue;
            if (stopId is not null && !view.StopIds.Contains(stopId)) continue;
            views.Add(view);
        }

        return views
            .OrderByDescending(v => v.IsActive)
            .ThenByDescending(v => LatestStart(v.Alert))
            .ThenBy(v => v.Alert.Id, StringComparer.Ordinal)
            .ToList();
    }

    private AlertView ToView(ServiceAlert alert, DateTimeOffset now)
    {
        var lines = new List<string>();
        foreach (var target in alert.Targets)
        {
            if (target.RouteId is not null) lines.Add(target.RouteId);
            if (target.TripId is not null && _tripLines.GetValueOrDefault(target.TripId) is { } line) lines.Add(line);
        }

        var invalid = alert.ActivePeriods.Any(IsInverted);
        return new AlertView(
            alert,
            lines.Distinct().ToList(),
            alert.Targets.Select(t => t.StopId).OfType<string>().Distinct().ToList(),
            alert.Targets.Select(t => t.TripId).OfType<string>().Distinct().ToList(),
            IsActive(alert, now),
            invalid);
    }

    private static bool IsInverted(AlertPeriod p) => p.Start is { } s && p.End is { } e && e < s;

    private static bool IsActive(ServiceAlert alert, DateTimeOffset now)
    {
        if (alert.ActivePeriods.Count == 0) return true;
        // A period that ends before it starts is a publisher mistake; we show the alert rather than
        // lose it, so such a period counts as open-ended.
        return alert.ActivePeriods.Any(p =>
            (p.Start is null || p.Start <= now) && (IsInverted(p) || p.End is null || now <= p.End));
    }

    private static DateTimeOffset LatestStart(ServiceAlert alert) =>
        alert.ActivePeriods.Select(p => p.Start ?? DateTimeOffset.MinValue).DefaultIfEmpty(DateTimeOffset.MinValue).Max();

    private static List<ServiceAlert> Normalize(IEnumerable<ServiceAlert> raw)
    {
        // Last one wins when the feed repeats an id.
        var byId = new Dictionary<string, ServiceAlert>(StringComparer.Ordinal);
        foreach (var alert in raw)
        {
            if (string.IsNullOrWhiteSpace(alert.Id)) continue;
            byId[alert.Id] = alert with
            {
                Header = NormalizeTexts(alert.Header),
                Description = NormalizeTexts(alert.Description),
            };
        }
        return byId.Values.ToList();
    }

    private static List<AlertText> NormalizeTexts(IEnumerable<AlertText> texts) =>
        texts.Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => AlertLanguage.Normalize(t.Text, t.Language))
            .ToList();

    /// <summary>Hash of everything a client sees, independent of the order alerts arrive in.</summary>
    private static string Fingerprint(IEnumerable<ServiceAlert> alerts)
    {
        var text = new StringBuilder();
        foreach (var a in alerts.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            text.Append("A|").Append(a.Id).Append('|').Append(a.Url).Append('\n');
            foreach (var p in a.ActivePeriods) text.Append("P|").Append(p.Start?.ToUnixTimeSeconds()).Append('|').Append(p.End?.ToUnixTimeSeconds()).Append('\n');
            foreach (var t in a.Targets) text.Append("T|").Append(t.RouteId).Append('|').Append(t.TripId).Append('|').Append(t.StopId).Append('\n');
            foreach (var h in a.Header) text.Append("H|").Append(h.Language).Append('|').Append(h.Text).Append('\n');
            foreach (var d in a.Description) text.Append("D|").Append(d.Language).Append('|').Append(d.Text).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <returns>True when a trip's line became known, which changes what a line filter returns.</returns>
    private async Task<bool> ResolveTripsAsync(IReadOnlyList<ServiceAlert> alerts, CancellationToken cancellationToken)
    {
        var referenced = alerts.SelectMany(a => a.Targets).Select(t => t.TripId).OfType<string>().ToHashSet(StringComparer.Ordinal);

        foreach (var known in _tripLines.Keys)
            if (!referenced.Contains(known)) _tripLines.TryRemove(known, out _);

        var unknown = referenced.Where(id => !_tripLines.ContainsKey(id)).ToList();
        var learned = 0;
        await Parallel.ForEachAsync(
            unknown,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentLookups, CancellationToken = cancellationToken },
            async (tripId, ct) =>
            {
                try
                {
                    var route = await trips.GetAsync(tripId, ct);
                    _tripLines[tripId] = route?.Line?.Id;
                    if (route?.Line is not null) Interlocked.Increment(ref learned);
                }
                catch (TransitCatalogException)
                {
                    // Left unresolved; the next cycle asks again.
                }
            });
        return learned > 0;
    }

    private void Publish(long version)
    {
        foreach (var publisher in publishers) publisher.PublishAlertsVersion(version);
    }
}
