using VehicleTracking.Application.Live;

namespace VehicleTracking.Application.Abstractions;

/// <summary>Receives every live change. Must return quickly and never block on a client.</summary>
public interface ILivePublisher
{
    void Publish(LiveChange change);
}
