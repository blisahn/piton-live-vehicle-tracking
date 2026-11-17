using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Infrastructure.Transit;

public sealed class JourneyPlannerOptions
{
    public const string SectionName = "JourneyPlanner";

    /// <summary>The GraphQL endpoint of Entur's Journey Planner.</summary>
    [Required, Url]
    public string Url { get; set; } = string.Empty;
}
