using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>Where and how often the vehicle positions feed is read.</summary>
public sealed class FeedOptions
{
    public const string SectionName = "Feed";

    [Required, Url]
    public string VehiclePositionsUrl { get; init; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan PollInterval { get; init; }

    /// <summary>Sent as the ET-Client-Name header, which Entur asks every consumer to set.</summary>
    [Required]
    public string ClientName { get; init; } = string.Empty;
}
