namespace VehicleTracking.Application.Alerts;

/// <summary>One service alert as the feed publishes it, after texts are normalized.</summary>
public sealed record ServiceAlert(
    string Id,
    IReadOnlyList<AlertPeriod> ActivePeriods,
    IReadOnlyList<AlertTarget> Targets,
    IReadOnlyList<AlertText> Header,
    IReadOnlyList<AlertText> Description,
    string? Url);

/// <summary>A null bound is open: no start means "since forever", no end means "until further notice".</summary>
public sealed record AlertPeriod(DateTimeOffset? Start, DateTimeOffset? End);

/// <summary>One thing the alert is about. The feed often names only a trip, not the line it belongs to.</summary>
public sealed record AlertTarget(string? RouteId, string? TripId, string? StopId);

/// <param name="Language">Short tag such as "no" or "en"; null when the feed gave none and none could be guessed.</param>
/// <param name="LanguageDetected">True when <paramref name="Language"/> was guessed from the text, not given by the feed.</param>
public sealed record AlertText(string Text, string? Language, bool LanguageDetected);

/// <summary>An alert together with what the service derived from it for filtering and display.</summary>
/// <param name="LineIds">Routes named directly plus the lines of the trips named.</param>
/// <param name="HasInvalidPeriod">Some period ends before it starts. It is kept and treated as open-ended.</param>
public sealed record AlertView(
    ServiceAlert Alert,
    IReadOnlyList<string> LineIds,
    IReadOnlyList<string> StopIds,
    IReadOnlyList<string> TripIds,
    bool IsActive,
    bool HasInvalidPeriod);

/// <summary>What the status screen shows about the alert feed.</summary>
public sealed record AlertFeedStatus(
    DateTimeOffset? LastSuccessfulReadAt,
    string? LastError,
    long SuccessfulReads,
    long FailedReads,
    int AlertCount);
