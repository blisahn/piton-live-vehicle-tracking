using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Application.Transit;

/// <summary>
/// Planned routes, fetched when a client asks for one and then kept in memory: a trip's route
/// does not change while the trip runs, and the same vehicle tends to be clicked again and again.
/// </summary>
public sealed class TripRouteService : IDisposable
{
    /// <summary>Longer than any single trip, so a route is fetched at most about once per trip.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    /// <summary>A few hundred routes of a few hundred points each: a few megabytes at most.</summary>
    private const int MaxEntries = 500;

    private readonly ITransitCatalog _catalog;
    private readonly ReferenceDataService _referenceData;

    // MemoryCache handles expiry and the size limit. It is private to this service, so its
    // size limit (counted in entries here) cannot clash with other users of a shared cache.
    private readonly MemoryCache _cache;
    private readonly object _createLock = new();

    public TripRouteService(ITransitCatalog catalog, ReferenceDataService referenceData, TimeProvider time)
    {
        _catalog = catalog;
        _referenceData = referenceData;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries, Clock = new TimeProviderClock(time) });
    }

    /// <returns>The route, or null when the catalog does not know the trip.</returns>
    /// <exception cref="TransitCatalogException">The catalog could not be reached. Failures are not cached.</exception>
    public async Task<TripRoute?> GetAsync(string tripId, CancellationToken cancellationToken)
    {
        var request = GetOrStartRequest(tripId);
        try
        {
            // The shared request is not cancelled when one caller gives up; only this caller stops waiting.
            return await request.Value.WaitAsync(cancellationToken);
        }
        catch (TransitCatalogException)
        {
            RemoveIfStillCached(tripId, request);
            throw;
        }
    }

    /// <summary>
    /// The cache holds the request, not its result, so callers arriving while it runs share it.
    /// IMemoryCache.GetOrCreate is not atomic: two callers that miss at the same moment would each
    /// create and start their own request. The lock makes "look up, else create" one step. It only
    /// covers creating a Lazy, never the request itself, so it is held for microseconds.
    /// </summary>
    private Lazy<Task<TripRoute?>> GetOrStartRequest(string tripId)
    {
        lock (_createLock)
        {
            if (_cache.TryGetValue(tripId, out Lazy<Task<TripRoute?>>? cached) && cached is not null)
                return cached;

            // Lazy runs Fetch at most once, however many callers ask for its Value.
            var request = new Lazy<Task<TripRoute?>>(() => Fetch(tripId));
            _cache.Set(tripId, request, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = Lifetime,
            });
            return request;
        }
    }

    private void RemoveIfStillCached(string tripId, Lazy<Task<TripRoute?>> failed)
    {
        lock (_createLock)
        {
            // Another caller may already have replaced the failed request with a new one; keep that.
            if (_cache.TryGetValue(tripId, out Lazy<Task<TripRoute?>>? cached) && ReferenceEquals(cached, failed))
                _cache.Remove(tripId);
        }
    }

    private async Task<TripRoute?> Fetch(string tripId)
    {
        var route = await _catalog.GetTripRouteAsync(tripId, CancellationToken.None);
        if (route is not null)
            _referenceData.Remember(route.Line is { } line ? [line] : [], route.Stops);
        return route;
    }

    public void Dispose() => _cache.Dispose();

    /// <summary>Lets the cache use the application's clock, so expiry follows the same time as everything else (and tests can move it).</summary>
    private sealed class TimeProviderClock(TimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
}
