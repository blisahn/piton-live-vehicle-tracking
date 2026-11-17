using VehicleTracking.Application.Abstractions;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Turns one raw feed response into at most one usable position per vehicle.
/// Every rule here comes from watching the real feed; see docs/measurements.
/// </summary>
public sealed class SnapshotNormalizer
{
    /// <summary>Vehicle clocks drift; a measurement slightly ahead of ours is still accepted.</summary>
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The feed sends coordinates as 32-bit floats (about 7 significant digits). Widening them to
    /// double shows digits that were never measured; 6 decimals (about 11 cm) keeps everything real.
    /// </summary>
    private const int CoordinateDecimals = 6;

    public NormalizedSnapshot Normalize(FeedSnapshot snapshot, DateTimeOffset now)
    {
        var newestPerVehicle = new Dictionary<string, VehiclePosition>();
        var rejected = 0;

        foreach (var vehicle in snapshot.Vehicles)
        {
            if (!TryCreatePosition(vehicle, now, out var position))
            {
                rejected++;
                continue;
            }

            // The feed keeps one entity per trip a vehicle served today, so the same vehicle
            // shows up several times. Only its newest measurement describes where it is now.
            if (!newestPerVehicle.TryGetValue(position.VehicleKey, out var known) || position.IsNewerThan(known))
                newestPerVehicle[position.VehicleKey] = position;
        }

        return new NormalizedSnapshot(newestPerVehicle.Values.ToList(), snapshot.Vehicles.Count, rejected);
    }

    private static bool TryCreatePosition(FeedVehicle vehicle, DateTimeOffset now, out VehiclePosition position)
    {
        position = null!;

        if (string.IsNullOrEmpty(vehicle.VehicleId)) return false;

        // Without its own timestamp a position cannot be ordered or de-duplicated.
        // Substituting the read time would invent a new measurement on every poll.
        if (vehicle.Timestamp is not { } recordedAt) return false;
        if (recordedAt > now + ClockSkewTolerance) return false;

        if (vehicle.Latitude is not { } latitude || vehicle.Longitude is not { } longitude) return false;
        if (!GeoCoordinate.IsUsable(latitude, longitude)) return false;

        position = new VehiclePosition(
            VehicleKey.Create(CodespaceOf(vehicle.EntityId), vehicle.VehicleId),
            recordedAt,
            Math.Round(latitude, CoordinateDecimals),
            Math.Round(longitude, CoordinateDecimals),
            NormalizeBearing(vehicle.Bearing),
            vehicle.Speed,
            NullIfEmpty(vehicle.RouteId),
            NullIfEmpty(vehicle.TripId),
            NullIfEmpty(vehicle.StopId),
            vehicle.StopStatus,
            vehicle.Occupancy);
        return true;
    }

    /// <summary>
    /// GTFS-Realtime defines bearing as 0–360 degrees clockwise from north, but some operators
    /// publish -180…180 and some publish exactly 360. Both are folded into [0, 360).
    /// </summary>
    private static float? NormalizeBearing(float? bearing)
    {
        if (bearing is not { } degrees || float.IsNaN(degrees) || float.IsInfinity(degrees)) return null;
        return ((degrees % 360) + 360) % 360;
    }

    /// <summary>Entity ids look like "RUT:ServiceJourney:…": the part before the first colon is the operator's codespace.</summary>
    private static string CodespaceOf(string entityId)
    {
        var separator = entityId.IndexOf(':');
        return separator > 0 ? entityId[..separator] : string.Empty;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <param name="Positions">One position per vehicle.</param>
/// <param name="EntityCount">How many entities the feed response contained.</param>
/// <param name="RejectedCount">Entities that could not be used (no id, no timestamp, no usable coordinates, from the future).</param>
public sealed record NormalizedSnapshot(IReadOnlyCollection<VehiclePosition> Positions, int EntityCount, int RejectedCount);
