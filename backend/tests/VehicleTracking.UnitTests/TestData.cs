using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.UnitTests;

/// <summary>Small builders so each test only states the values it cares about.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static FeedVehicle Feed(
        string entityId = "RUT:ServiceJourney:1-a",
        string? vehicleId = "101",
        DateTimeOffset? timestamp = null,
        double? lat = 59.9,
        double? lon = 10.7,
        float? bearing = null,
        string? routeId = null,
        string? tripId = null,
        string? stopId = null,
        bool noTimestamp = false) =>
        new(entityId, vehicleId, noTimestamp ? null : timestamp ?? Now.AddSeconds(-10), lat, lon,
            bearing, null, routeId, tripId, stopId, null, null);

    public static VehiclePosition Position(
        string key = "RUT:101", DateTimeOffset? at = null, double lat = 59.9, double lon = 10.7, string? routeId = null) =>
        new(key, at ?? Now, lat, lon, null, null, routeId, null, null, null, null);

    public static FeedSnapshot Snapshot(params FeedVehicle[] vehicles) => new(Now, vehicles);
}
