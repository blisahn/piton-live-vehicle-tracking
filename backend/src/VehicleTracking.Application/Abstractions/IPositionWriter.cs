using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Abstractions;

public interface IPositionWriter
{
    /// <summary>
    /// Writes a batch in one transaction. Writing the same batch twice is harmless:
    /// a position that already exists is skipped. Returns how many rows were actually inserted.
    /// </summary>
    Task<int> WriteAsync(IReadOnlyCollection<VehiclePosition> batch, CancellationToken cancellationToken);
}
