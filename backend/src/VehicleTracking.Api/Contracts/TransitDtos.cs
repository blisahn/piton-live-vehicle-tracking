using VehicleTracking.Application.Transit;

namespace VehicleTracking.Api.Contracts;

public sealed record LineDto(string Id, string? PublicCode, string? Name, string? TransportMode)
{
    public static LineDto From(TransitLine line) => new(line.Id, line.PublicCode, line.Name, line.TransportMode);
}

public sealed record StopDto(string Id, string Name, double Lat, double Lon)
{
    public static StopDto From(TransitStop stop) => new(stop.Id, stop.Name, stop.Latitude, stop.Longitude);
}

public sealed record LineListDto(IReadOnlyList<LineDto> Lines);

public sealed record StopListDto(IReadOnlyList<StopDto> Stops);

public sealed record PointDto(double Lat, double Lon);

/// <param name="PathIsApproximate">True when <paramref name="Path"/> only joins the stops with straight lines.</param>
public sealed record TripRouteDto(
    string TripId,
    LineDto? Line,
    IReadOnlyList<PointDto> Path,
    IReadOnlyList<StopDto> Stops,
    bool PathIsApproximate)
{
    public static TripRouteDto From(TripRoute route) => new(
        route.TripId,
        route.Line is { } line ? LineDto.From(line) : null,
        route.Path.Select(point => new PointDto(point.Latitude, point.Longitude)).ToList(),
        route.Stops.Select(StopDto.From).ToList(),
        route.PathIsApproximate);
}
