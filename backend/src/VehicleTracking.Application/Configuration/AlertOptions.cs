using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>Where and how often the service alerts feed is read. Alerts change slowly, so this is much lazier than the vehicle feed.</summary>
public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    [Required, Url]
    public string Url { get; init; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan PollInterval { get; init; }
}
