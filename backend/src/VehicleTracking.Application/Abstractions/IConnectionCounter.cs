namespace VehicleTracking.Application.Abstractions;

public interface IConnectionCounter
{
    /// <summary>WebSocket clients connected right now.</summary>
    int ConnectedClients { get; }
}
