using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

public interface ILatestPositionReader
{
    /// <summary>The newest stored position of every vehicle that reported at or after <paramref name="since"/>.</summary>
    Task<IReadOnlyList<VehiclePosition>> ReadLatestPerVehicleAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
