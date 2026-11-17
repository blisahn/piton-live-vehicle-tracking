using VehicleTracking.Application.Transit;

namespace VehicleTracking.Infrastructure.Transit;

/// <summary>
/// Decodes a Google encoded polyline with 5 decimals of precision.
/// The idea (https://developers.google.com/maps/documentation/utilities/polylinealgorithm):
/// every number is split into 5-bit chunks stored as printable characters, the sign is folded into the
/// lowest bit (zig-zag), and every point is stored as the difference to the previous one.
/// </summary>
internal static class PolylineDecoder
{
    public static IReadOnlyList<GeoPoint> Decode(string encoded)
    {
        var points = new List<GeoPoint>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encoded.Length)
        {
            latitude += ReadNumber(encoded, ref index);
            longitude += ReadNumber(encoded, ref index);
            points.Add(new GeoPoint(latitude / 1e5, longitude / 1e5));
        }

        return points;
    }

    private static int ReadNumber(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;
        do
        {
            // Characters start at '?' (63); a chunk with bit 0x20 set means more chunks follow.
            chunk = encoded[index++] - 63;
            result |= (chunk & 0x1f) << shift;
            shift += 5;
        }
        while (chunk >= 0x20);

        // Undo the zig-zag: an odd value is a negative number.
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
