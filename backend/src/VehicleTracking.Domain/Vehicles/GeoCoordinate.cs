namespace VehicleTracking.Domain.Vehicles;

public static class GeoCoordinate
{
    /// <summary>
    /// True for a position a vehicle can really be at. The feed uses (0, 0) and (-1, -1)
    /// as "no GPS fix" placeholders; both are inside the valid range, so they are rejected by value.
    /// </summary>
    public static bool IsUsable(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsNaN(longitude)) return false;
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return false;
        if (latitude == 0 && longitude == 0) return false;
        if (latitude == -1 && longitude == -1) return false;
        return true;
    }
}
