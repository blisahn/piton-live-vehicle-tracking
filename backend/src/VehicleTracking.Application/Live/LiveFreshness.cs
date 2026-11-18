namespace VehicleTracking.Application.Live;

/// <summary>
/// How recently the remembered vehicles last reported. The first three groups are on the live map;
/// <see cref="OffTheMap"/> went silent for longer than <see cref="StaleAfter"/> (usually a finished
/// trip) and is only remembered, so a repeated old position is not stored again.
/// </summary>
public sealed record LiveFreshness(
    int UnderOneMinute,
    int OneToFiveMinutes,
    int FiveMinutesToStale,
    int OffTheMap,
    TimeSpan StaleAfter);
