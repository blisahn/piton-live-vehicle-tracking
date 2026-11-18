import { isInside, type Bounds } from "./viewport";
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
  return countVehicles(vehicles, filter, lines, null).matching;
}

/**
 * How many vehicles match the filter, and how many of those are inside `screen`.
 * One pass of a few comparisons per vehicle: a few thousand vehicles take a fraction of a millisecond.
 */
export function countVehicles(
  vehicles: ReadonlyMap<string, Vehicle>,
  filter: string,
  lines: ReadonlyMap<string, Line>,
  screen: Bounds | null,
): { matching: number; onScreen: number } {
  let matching = 0;
  let onScreen = 0;
  for (const vehicle of vehicles.values()) {
    if (!matchesFilter(vehicle, filter, vehicle.routeId ? lines.get(vehicle.routeId) : undefined)) continue;
    matching++;
    if (screen && isInside(screen, vehicle.lon, vehicle.lat)) onScreen++;
  }
  return { matching, onScreen };
}

/** The kinds of vehicle the map tells apart. The feed itself has no type; it comes from the vehicle's line. */
export const VEHICLE_MODES = ["bus", "coach", "rail", "tram", "metro", "water", "unknown"] as const;
export type VehicleMode = (typeof VEHICLE_MODES)[number];

/** Journey Planner transport modes; anything else (or a vehicle without a known line) is "unknown". */
export function vehicleMode(line: Line | undefined): VehicleMode {
  const mode = line?.transportMode;
  return (VEHICLE_MODES as readonly string[]).includes(mode ?? "") ? (mode as VehicleMode) : "unknown";
}

export type VehicleProperties = { key: string; routeId: string | null; bearing: number | null; mode: VehicleMode };
export type VehicleCollection = GeoJSON.FeatureCollection<GeoJSON.Point, VehicleProperties>;

export function buildVehicleCollection(
  vehicles: ReadonlyMap<string, Vehicle>,
  filter: string,
  lines: ReadonlyMap<string, Line>,
): VehicleCollection {
  const features: VehicleCollection["features"] = [];
  for (const vehicle of vehicles.values()) {
    const line = vehicle.routeId ? lines.get(vehicle.routeId) : undefined;
    if (!matchesFilter(vehicle, filter, line)) continue;
    features.push({
      type: "Feature",
      // GeoJSON order is [longitude, latitude]
      geometry: { type: "Point", coordinates: [vehicle.lon, vehicle.lat] },
      properties: { key: vehicle.key, routeId: vehicle.routeId, bearing: vehicle.bearing, mode: vehicleMode(line) },
    });
  }
  return { type: "FeatureCollection", features };
}
