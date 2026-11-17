using VehicleTracking.Application.Alerts;

namespace VehicleTracking.Api.Contracts;

/// <param name="Version">Changes whenever the alerts change; the WebSocket announces the same number.</param>
/// <param name="GeneratedAt">When the feed said it was produced; null if it did not say.</param>
public sealed record AlertListDto(long Version, DateTimeOffset? GeneratedAt, IReadOnlyList<AlertDto> Alerts);

/// <param name="Active">Whether the alert applies right now.</param>
/// <param name="HasInvalidPeriod">A period ends before it starts; it is shown as open-ended.</param>
/// <param name="LineIds">Routes named by the alert plus the lines of the trips it names.</param>
public sealed record AlertDto(
    string Id,
    bool Active,
    bool HasInvalidPeriod,
    IReadOnlyList<AlertPeriodDto> ActivePeriods,
    IReadOnlyList<string> LineIds,
    IReadOnlyList<string> StopIds,
    IReadOnlyList<string> TripIds,
    IReadOnlyList<AlertTextDto> Header,
    IReadOnlyList<AlertTextDto> Description,
    string? Url)
{
    public static AlertDto From(AlertView view) => new(
        view.Alert.Id,
        view.IsActive,
        view.HasInvalidPeriod,
        view.Alert.ActivePeriods.Select(p => new AlertPeriodDto(p.Start, p.End)).ToList(),
        view.LineIds,
        view.StopIds,
        view.TripIds,
        view.Alert.Header.Select(AlertTextDto.From).ToList(),
        view.Alert.Description.Select(AlertTextDto.From).ToList(),
        view.Alert.Url);
}

/// <summary>A null bound is open.</summary>
public sealed record AlertPeriodDto(DateTimeOffset? Start, DateTimeOffset? End);

/// <param name="Language">"no", "en", another tag, or null when unknown.</param>
/// <param name="LanguageDetected">True when the language was guessed from the text.</param>
public sealed record AlertTextDto(string Text, string? Language, bool LanguageDetected)
{
    public static AlertTextDto From(AlertText text) => new(text.Text, text.Language, text.LanguageDetected);
}
