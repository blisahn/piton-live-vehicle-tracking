using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>Which vehicles count as live, as opposed to how long their positions are kept (<see cref="RetentionOptions"/>).</summary>
public sealed class LiveOptions
{
    public const string SectionName = "Live";

    /// <summary>
    /// A vehicle silent for longer than this leaves the live map; its positions stay in history.
    /// Many vehicles keep their last report in the feed for hours after a trip ends (Vy's vehicle
    /// id is really a trip id), which made up ~30–46 % of the map. Normal reports come every
    /// 10–60 s (99.9 % within 220 s) and the longest real silence measured was 8.5 min (the Follo
    /// Line tunnel), so 10 minutes removes finished trips without dropping vehicles in service.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromMinutes(10);
}
