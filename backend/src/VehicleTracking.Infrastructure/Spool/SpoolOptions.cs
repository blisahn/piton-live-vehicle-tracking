using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Infrastructure.Spool;

/// <summary>Where the on-disk spool keeps its batch files.</summary>
public sealed class SpoolOptions
{
    public const string SectionName = "Spool";

    [Required]
    public string Directory { get; init; } = string.Empty;
}
