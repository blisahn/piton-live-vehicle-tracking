namespace VehicleTracking.Application.Transit;

/// <summary>A line as passengers know it, e.g. public code "M1", name "Flekkerøy - Sørlandsparken".</summary>
/// <param name="Id">The id the live feed uses as route_id, e.g. "AKT:Line:1001".</param>
/// <param name="TransportMode">"bus", "rail", "tram", "water", … as Entur names them.</param>
public sealed record TransitLine(string Id, string? PublicCode, string? Name, string? TransportMode);

/// <param name="Id">The id the live feed uses as stop_id, e.g. "NSR:Quay:100140".</param>
public sealed record TransitStop(string Id, string Name, double Latitude, double Longitude);

public sealed record GeoPoint(double Latitude, double Longitude);

/// <summary>The planned route of one trip.</summary>
/// <param name="Path">The planned path along the roads or tracks, first stop to last.</param>
/// <param name="Stops">The stops the trip serves, in order.</param>
/// <param name="PathIsApproximate">
/// True when the source had no path for this trip and <see cref="Path"/> just joins the stops with straight lines.
/// </param>
public sealed record TripRoute(
    string TripId,
    TransitLine? Line,
    IReadOnlyList<GeoPoint> Path,
    IReadOnlyList<TransitStop> Stops,
    bool PathIsApproximate);
