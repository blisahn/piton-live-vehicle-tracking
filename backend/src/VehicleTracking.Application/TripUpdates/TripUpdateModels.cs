namespace VehicleTracking.Application.TripUpdates;

/// <summary>
/// One stop of a trip as the Trip Updates feed reports it. A struct, because the feed carries
/// hundreds of thousands of these and an object header per row would add up.
/// </summary>
/// <param name="Sequence">The stop's position in the trip as the feed numbers it (1-based in Entur's data).</param>
/// <param name="ArrivalDelay">Seconds late (negative: early); null when the feed gave none or it was implausible.</param>
public readonly record struct StopDelay(int Sequence, string StopId, int? ArrivalDelay, int? DepartureDelay, bool Skipped);

/// <param name="StartDate">The service day the trip belongs to; its stop times are relative to it.</param>
/// <param name="Canceled">The whole trip is canceled.</param>
public sealed record TripProgressRecord(
    string TripId,
    DateOnly? StartDate,
    string? RouteId,
    bool Canceled,
    DateTimeOffset? Timestamp,
    StopDelay[] Stops);

/// <param name="DroppedDelays">Delays discarded as implausible while reading this response.</param>
public sealed record TripUpdateSnapshot(
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<TripProgressRecord> Trips,
    int DroppedDelays);

/// <summary>What the status screen shows about the trip updates feed.</summary>
public sealed record TripUpdateStatus(
    DateTimeOffset? LastSuccessfulReadAt,
    string? LastError,
    long SuccessfulReads,
    long FailedReads,
    int TripCount,
    long DroppedDelays);

/// <summary>A trip's progress against its timetable, ready to show.</summary>
/// <param name="DelaySeconds">Delay at the next upcoming stop; null when the feed gave no delay at all.</param>
public sealed record TripProgressView(
    string TripId,
    DateOnly? StartDate,
    DateTimeOffset? UpdatedAt,
    bool Canceled,
    int? DelaySeconds,
    IReadOnlyList<StopProgress> Stops);

/// <param name="Name">Null when the planned route (and so the stop name) is unknown.</param>
public sealed record StopProgress(
    int Sequence,
    string StopId,
    string? Name,
    DateTimeOffset? AimedArrival,
    DateTimeOffset? ExpectedArrival,
    DateTimeOffset? AimedDeparture,
    DateTimeOffset? ExpectedDeparture,
    int? ArrivalDelaySeconds,
    int? DepartureDelaySeconds,
    bool Skipped);
