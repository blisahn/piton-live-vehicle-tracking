namespace VehicleTracking.Application.Abstractions;

public interface IWebSocketTraffic
{
    /// <summary>Bytes of message payload sent to all WebSocket clients during the last 60 seconds.</summary>
    long BytesSentLastMinute { get; }
}
