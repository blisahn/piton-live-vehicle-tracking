using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.IntegrationTests;

internal static class Positions
{
    /// <summary>Whole seconds: timestamptz keeps microseconds, so round-tripped values compare equal.</summary>
    public static DateTimeOffset Now() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public static VehiclePosition At(string vehicleKey, DateTimeOffset recordedAt, string? tripId = "ATB:ServiceJourney:1") =>
        new(vehicleKey, recordedAt, 63.43, 10.39, 90f, null, "ATB:Line:2_74", tripId, "NSR:Quay:1",
            VehicleStopStatus.InTransitTo, null);

    /// <summary>One position per second for one vehicle, oldest first, ending at <paramref name="newest"/>.</summary>
    public static List<VehiclePosition> Track(string vehicleKey, DateTimeOffset newest, int count) =>
        Enumerable.Range(0, count).Select(i => At(vehicleKey, newest.AddSeconds(i - count + 1))).ToList();
}
