using VehicleTracking.Application.Transit;
using VehicleTracking.Application.TripUpdates;

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

/// <summary>Where a trip stands against its timetable. Times are ISO 8601 with offset.</summary>
/// <param name="DelaySeconds">Delay at the next upcoming stop; negative is early, null when unknown.</param>
public sealed record TripProgressDto(
    string TripId,
    DateOnly? StartDate,
    DateTimeOffset? UpdatedAt,
    bool Canceled,
    int? DelaySeconds,
    IReadOnlyList<StopProgressDto> Stops)
{
    public static TripProgressDto From(TripProgressView view) => new(
        view.TripId,
        view.StartDate,
        view.UpdatedAt,
        view.Canceled,
        view.DelaySeconds,
        view.Stops.Select(StopProgressDto.From).ToList());
}

/// <param name="Name">Null when the planned route is unknown.</param>
/// <param name="AimedArrival">Timetable times and their expected counterparts are null when no timetable is known.</param>
public sealed record StopProgressDto(
    int Sequence,
    string StopId,
    string? Name,
    DateTimeOffset? AimedArrival,
    DateTimeOffset? ExpectedArrival,
    DateTimeOffset? AimedDeparture,
    DateTimeOffset? ExpectedDeparture,
    int? ArrivalDelaySeconds,
    int? DepartureDelaySeconds,
    bool Skipped)
{
    public static StopProgressDto From(StopProgress s) => new(
        s.Sequence, s.StopId, s.Name, s.AimedArrival, s.ExpectedArrival, s.AimedDeparture, s.ExpectedDeparture,
        s.ArrivalDelaySeconds, s.DepartureDelaySeconds, s.Skipped);
}
