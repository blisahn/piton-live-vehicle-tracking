using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>Where and how often the trip updates feed is read. Each read is about 12 MB, so it is polled lazily.</summary>
public sealed class TripUpdateOptions
{
    public const string SectionName = "TripUpdates";

    [Required, Url]
    public string Url { get; init; } = "https://api.entur.io/realtime/v1/gtfs-rt/trip-updates";

    [Range(typeof(TimeSpan), "00:00:10", "00:10:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);
}
