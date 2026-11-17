"use client";

import { useMemo, useState } from "react";
import { useSelectedVehicle } from "@/entities/vehicle";
import {
  LiveMap,
  VehicleFilter,
  useVehicleFilter,
  type RouteOverlay,
  type TrackOverlay,
} from "@/features/live-map";
import { VehicleAlerts } from "@/features/alerts";
import { VehiclePanel } from "@/features/vehicle-detail";
import { HistoryPanel } from "@/features/vehicle-history";
import { RouteSection } from "@/features/vehicle-route";

/**
 * The live map screen. Features do not know about each other; this is the one place that
 * connects them: the selected vehicle and the filter come from the URL, and the history
 * feature hands its track to the map through state held here.
 */
export function LiveMapScreen() {
  const [selectedKey, selectVehicle] = useSelectedVehicle();
  const [filter] = useVehicleFilter();
  const [track, setTrack] = useState<TrackOverlay | null>(null);
  const [route, setRoute] = useState<RouteOverlay | null>(null);

  // Drop the previous vehicle's overlays during render (React's "adjust state on prop change" pattern).
  const [overlaysFor, setOverlaysFor] = useState(selectedKey);
  if (overlaysFor !== selectedKey) {
    setOverlaysFor(selectedKey);
    setTrack(null);
    setRoute(null);
  }

  // The measured path follows the planned route where the vehicle was on it.
  // An approximate route is only straight lines between stops; snapping real positions to it would be wrong.
  const snapPath = useMemo(
    () => (route && !route.approximate ? [...route.passed, ...route.remaining] : null),
    [route],
  );

  return (
    // Phone: map on top, panel below as a sheet. From md up: panel beside the map.
    <div className="flex min-h-0 flex-1 flex-col md:flex-row">
      <section className="relative min-h-0 flex-1">
        <div className="absolute inset-x-2 top-2 z-10 rounded-lg border bg-background/95 p-2 shadow-sm md:right-auto md:w-80">
          <VehicleFilter />
        </div>
        <LiveMap selectedKey={selectedKey} onSelect={selectVehicle} filter={filter} track={track} route={route} />
      </section>

      {selectedKey ? (
        <aside className="flex max-h-[55dvh] min-h-0 flex-col border-t md:max-h-none md:w-96 md:border-l md:border-t-0">
          <VehiclePanel vehicleKey={selectedKey} onClose={() => selectVehicle(null)}>
            <VehicleAlerts vehicleKey={selectedKey} />
            <RouteSection vehicleKey={selectedKey} onRouteChange={setRoute} />
            <HistoryPanel vehicleKey={selectedKey} onTrackChange={setTrack} snapPath={snapPath} />
          </VehiclePanel>
        </aside>
      ) : null}
    </div>
  );
}
