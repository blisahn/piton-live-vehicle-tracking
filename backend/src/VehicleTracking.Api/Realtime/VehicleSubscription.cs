using System.Globalization;
using System.Text.Json;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Realtime;

/// <summary>Geographic rectangle, edges inclusive.</summary>
internal sealed record BoundingBox(double West, double South, double East, double North)
{
    public static bool TryCreate(double west, double south, double east, double north, out BoundingBox? box)
    {
        box = null;
        if (!double.IsFinite(west) || !double.IsFinite(south) || !double.IsFinite(east) || !double.IsFinite(north)) return false;
        if (south < -90 || north > 90 || west < -180 || east > 180) return false;
        if (south > 90 || north < -90 || west > 180 || east < -180) return false;
        if (!(west < east) || !(south < north)) return false;
        box = new BoundingBox(west, south, east, north);
        return true;
    }

    public bool Contains(double lat, double lon) => lon >= West && lon <= East && lat >= South && lat <= North;
}

/// <summary>
/// What one client wants to see. Immutable; see WireMessages.cs for the protocol and the match rule:
/// key in Keys, else routeId in Lines when Lines is not empty, else inside Bbox, else everything.
/// </summary>
internal sealed record VehicleSubscription(BoundingBox? Bbox, IReadOnlySet<string> Lines, IReadOnlySet<string> Keys)
{
    public const int MaxLines = 500;
    public const int MaxKeys = 20;

    public static readonly VehicleSubscription Everything =
        new(null, new HashSet<string>(), new HashSet<string>());

    public bool Matches(VehiclePosition position)
    {
        if (Keys.Contains(position.VehicleKey)) return true;
        if (Lines.Count > 0) return position.RouteId is { } route && Lines.Contains(route);
        if (Bbox is not null) return Bbox.Contains(position.Latitude, position.Longitude);
        return true;
    }

    /// <summary>Parses the body of a "subscribe" message. Missing or null fields mean "not set".</summary>
    public static bool TryParse(JsonElement message, out VehicleSubscription subscription)
    {
        subscription = Everything;
        if (message.ValueKind != JsonValueKind.Object) return false;

        BoundingBox? bbox = null;
        if (message.TryGetProperty("bbox", out var bboxElement) && bboxElement.ValueKind != JsonValueKind.Null)
        {
            if (bboxElement.ValueKind != JsonValueKind.Array || bboxElement.GetArrayLength() != 4) return false;
            Span<double> n = stackalloc double[4];
            var i = 0;
            foreach (var item in bboxElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out n[i++])) return false;
            }
            if (!BoundingBox.TryCreate(n[0], n[1], n[2], n[3], out bbox)) return false;
        }

        if (!TryReadStrings(message, "lines", MaxLines, out var lines)) return false;
        if (!TryReadStrings(message, "keys", MaxKeys, out var keys)) return false;

        subscription = new VehicleSubscription(bbox, lines, keys);
        return true;
    }

    private static bool TryReadStrings(JsonElement message, string name, int max, out HashSet<string> values)
    {
        values = new HashSet<string>();
        if (!message.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > max) return false;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return false;
            var text = item.GetString();
            if (!string.IsNullOrEmpty(text)) values.Add(text);
        }
        return true;
    }

    /// <summary>
    /// Parses the connect-time query string: bbox=w,s,e,n, lines=a,b, keys=k1,k2 (all optional,
    /// comma-separated, already URL-decoded; ids may contain colons but never commas).
    /// </summary>
    public static bool TryFromQuery(IQueryCollection query, out VehicleSubscription subscription) =>
        TryFromQueryValues(query["bbox"].ToString(), query["lines"].ToString(), query["keys"].ToString(), out subscription);

    public static bool TryFromQueryValues(string? bbox, string? lines, string? keys, out VehicleSubscription subscription)
    {
        subscription = Everything;

        BoundingBox? box = null;
        if (!string.IsNullOrWhiteSpace(bbox))
        {
            var parts = bbox.Split(',');
            if (parts.Length != 4) return false;
            var n = new double[4];
            for (var i = 0; i < 4; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i])) return false;
            }
            if (!BoundingBox.TryCreate(n[0], n[1], n[2], n[3], out box)) return false;
        }

        var lineSet = Split(lines);
        var keySet = Split(keys);
        if (lineSet.Count > MaxLines || keySet.Count > MaxKeys) return false;

        subscription = new VehicleSubscription(box, lineSet, keySet);
        return true;
    }

    private static HashSet<string> Split(string? value) =>
        string.IsNullOrEmpty(value)
            ? new HashSet<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
}
