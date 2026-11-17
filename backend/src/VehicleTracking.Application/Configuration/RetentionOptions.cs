using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>How long recorded positions are kept before they are dropped.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan Period { get; init; }
}
