using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Transit;

namespace VehicleTracking.Application.TripUpdates;

/// <summary>
/// Joins the delays from the Trip Updates feed with the planned stop times of the trip's route.
/// The feed only says "N seconds late"; the timetable says when the stop was due, so the
/// expected time is the one plus the other.
/// </summary>
public sealed class TripProgressService(TripUpdateService updates, TripRouteService routes)
{
    // Timetables are published in local time; the service day only has meaning in this zone.
    // Resolved once: it needs tzdata on the host, which the runtime image has.
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    /// <returns>Null when the trip is not in the Trip Updates feed.</returns>
    public async Task<TripProgressView?> GetAsync(string tripId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var record = updates.TryGet(tripId);
        if (record is null) return null;

        // The route is usually cached already (the map fetched it); if it cannot be had, the delays alone are still useful.
        TripRoute? route = null;
        try
        {
            route = await routes.GetAsync(tripId, cancellationToken);
        }
        catch (TransitCatalogException)
        {
        }

        return Build(record, route, now);
    }

    internal static TripProgressView Build(TripProgressRecord record, TripRoute? route, DateTimeOffset now)
    {
        var planned = Plan(record.StartDate, route);

        var stops = new List<StopProgress>(record.Stops.Length);
        int? carried = null; // delay of the latest earlier stop, which later stops without their own delay inherit
        foreach (var stop in record.Stops)
        {
            var match = Match(stop, planned);
            var (aimedArrival, aimedDeparture, name) = match is { } m
                ? (m.AimedArrival, m.AimedDeparture, m.Name)
                : (null, null, null);

            // Propagation follows what the vehicle does next: its departure delay.
            // A stop with only a departure delay (often the first one) is better described by it than by an earlier stop.
            var arrivalDelay = stop.ArrivalDelay ?? stop.DepartureDelay ?? carried;
            var departureDelay = stop.DepartureDelay ?? carried;
            if (!stop.Skipped && (stop.DepartureDelay ?? stop.ArrivalDelay) is { } own) carried = own;

            stops.Add(new StopProgress(
                stop.Sequence,
                stop.StopId,
                name,
                aimedArrival,
                Shift(aimedArrival, arrivalDelay),
                aimedDeparture,
                Shift(aimedDeparture, departureDelay),
                arrivalDelay,
                departureDelay,
                stop.Skipped));
        }

        return new TripProgressView(
            record.TripId, record.StartDate, record.Timestamp, record.Canceled, DelayAt(stops, now), stops);
    }

    private static DateTimeOffset? Shift(DateTimeOffset? aimed, int? delay) =>
        aimed is { } time ? time.AddSeconds(delay ?? 0) : null;

    /// <summary>The delay at the first stop not yet passed, else at the last stop.</summary>
    private static int? DelayAt(List<StopProgress> stops, DateTimeOffset now)
    {
        var withDelay = stops.Where(s => !s.Skipped && (s.ArrivalDelaySeconds ?? s.DepartureDelaySeconds) is not null).ToList();
        if (withDelay.Count == 0) return null;

        foreach (var stop in stops)
        {
            if (stop.Skipped) continue;
            var due = stop.ExpectedDeparture ?? stop.ExpectedArrival ?? stop.AimedDeparture ?? stop.AimedArrival;
            if (due is { } at && at >= now)
                return stop.ArrivalDelaySeconds ?? stop.DepartureDelaySeconds;
        }

        var last = withDelay[^1];
        return last.ArrivalDelaySeconds ?? last.DepartureDelaySeconds;
    }

    private sealed record PlannedStop(string StopId, string Name, DateTimeOffset? AimedArrival, DateTimeOffset? AimedDeparture);

    /// <summary>The route's stops with absolute aimed times; empty when there is no route.</summary>
    private static List<PlannedStop> Plan(DateOnly? startDate, TripRoute? route)
    {
        var result = new List<PlannedStop>();
        if (route is null) return result;

        var times = route.StopTimes;
        var dayOffset = 0;
        TimeOnly? previous = null;
        for (var i = 0; i < route.Stops.Count; i++)
        {
            var stop = route.Stops[i];
            var time = times is not null && i < times.Count && times[i].StopId == stop.Id ? times[i] : null;

            DateTimeOffset? arrival = null, departure = null;
            if (startDate is { } date && time is not null)
            {
                // The clock going backwards between consecutive times means the trip passed midnight.
                (arrival, dayOffset, previous) = Resolve(date, time.AimedArrival, dayOffset, previous);
                (departure, dayOffset, previous) = Resolve(date, time.AimedDeparture, dayOffset, previous);
            }
            result.Add(new PlannedStop(stop.Id, stop.Name, arrival, departure));
        }
        return result;
    }

    private static (DateTimeOffset? Time, int DayOffset, TimeOnly? Previous) Resolve(
        DateOnly date, TimeOnly? local, int dayOffset, TimeOnly? previous)
    {
        if (local is not { } t) return (null, dayOffset, previous);
        if (previous is { } p && t < p) dayOffset++;
        var wall = date.AddDays(dayOffset).ToDateTime(t, DateTimeKind.Unspecified);
        // A wall time skipped by the spring clock change does not exist; read it as the hour after.
        if (Oslo.IsInvalidTime(wall)) wall = wall.AddHours(1);
        var offset = Oslo.GetUtcOffset(wall);
        return (new DateTimeOffset(wall, offset), dayOffset, t);
    }

    /// <summary>The feed stop's planned counterpart: by position when the id agrees, otherwise by id.</summary>
    private static PlannedStop? Match(StopDelay stop, List<PlannedStop> planned)
    {
        var index = stop.Sequence - 1;
        if (index >= 0 && index < planned.Count && planned[index].StopId == stop.StopId) return planned[index];
        return planned.FirstOrDefault(p => p.StopId == stop.StopId);
    }
}
