namespace VehicleTracking.Domain.Vehicles;

/// <summary>Where the vehicle is relative to its current stop. Values match GTFS-Realtime VehicleStopStatus.</summary>
public enum VehicleStopStatus
{
    IncomingAt = 0,
    StoppedAt = 1,
    InTransitTo = 2,
}
