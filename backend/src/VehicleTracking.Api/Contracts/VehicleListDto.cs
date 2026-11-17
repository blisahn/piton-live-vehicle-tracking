namespace VehicleTracking.Api.Contracts;

/// <summary>The latest position of every (matching) vehicle as of <paramref name="Sequence"/>.</summary>
public sealed record VehicleListDto(long Sequence, int Count, IReadOnlyList<VehicleDto> Vehicles);
