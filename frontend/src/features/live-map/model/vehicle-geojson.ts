import type { Vehicle } from "@/entities/vehicle";
import { lineMatches, type Line } from "@/entities/transit";

/**
 * Case-insensitive "contains" on the vehicle key, its route id, and its line's public code or name.
 * Empty filter matches all. The line is passed in (not looked up) so this stays a pure function.
 */
export function matchesFilter(vehicle: Vehicle, filter: string, line: Line | undefined): boolean {
  const needle = filter.trim().toLowerCase();
  if (needle === "") return true;
  return (
    vehicle.key.toLowerCase().includes(needle) ||
    (vehicle.routeId?.toLowerCase().includes(needle) ?? false) ||
    lineMatches(line, needle)
  );
}

export function countMatches(
  vehicles: ReadonlyMap<string, Vehicle>,
  filter: string,
  lines: ReadonlyMap<string, Line>,
): number {
  let count = 0;
  for (const vehicle of vehicles.values()) {
    if (matchesFilter(vehicle, filter, vehicle.routeId ? lines.get(vehicle.routeId) : undefined)) count++;
  }
  return count;
}

export type VehicleProperties = { key: string; routeId: string | null; bearing: number | null };
export type VehicleCollection = GeoJSON.FeatureCollection<GeoJSON.Point, VehicleProperties>;

export function buildVehicleCollection(
  vehicles: ReadonlyMap<string, Vehicle>,
  filter: string,
  lines: ReadonlyMap<string, Line>,
): VehicleCollection {
  const features: VehicleCollection["features"] = [];
  for (const vehicle of vehicles.values()) {
    if (!matchesFilter(vehicle, filter, vehicle.routeId ? lines.get(vehicle.routeId) : undefined)) continue;
    features.push({
      type: "Feature",
      // GeoJSON order is [longitude, latitude]
      geometry: { type: "Point", coordinates: [vehicle.lon, vehicle.lat] },
      properties: { key: vehicle.key, routeId: vehicle.routeId, bearing: vehicle.bearing },
    });
  }
  return { type: "FeatureCollection", features };
}
