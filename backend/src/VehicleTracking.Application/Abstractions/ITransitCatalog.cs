using VehicleTracking.Application.Transit;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Names and planned routes, looked up by the ids the live feed carries.</summary>
public interface ITransitCatalog
{
    /// <summary>Ids the catalog does not know are left out of the result.</summary>
    /// <exception cref="TransitCatalogException">The catalog could not be reached.</exception>
    Task<IReadOnlyList<TransitLine>> GetLinesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>Ids the catalog does not know are left out of the result.</summary>
    /// <exception cref="TransitCatalogException">The catalog could not be reached.</exception>
    Task<IReadOnlyList<TransitStop>> GetStopsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <returns>The route, or null when the catalog does not know the trip.</returns>
    /// <exception cref="TransitCatalogException">The catalog could not be reached.</exception>
    Task<TripRoute?> GetTripRouteAsync(string tripId, CancellationToken cancellationToken);
}

public sealed class TransitCatalogException(string message, Exception? inner = null) : Exception(message, inner);
