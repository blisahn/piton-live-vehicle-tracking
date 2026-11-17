namespace VehicleTracking.Domain.Vehicles;

/// <summary>How full the vehicle is. Values match GTFS-Realtime OccupancyStatus.</summary>
public enum OccupancyStatus
{
    Empty = 0,
    ManySeatsAvailable = 1,
    FewSeatsAvailable = 2,
    StandingRoomOnly = 3,
    CrushedStandingRoomOnly = 4,
    Full = 5,
    NotAcceptingPassengers = 6,
    NoDataAvailable = 7,
    NotBoardable = 8,
}
