"use client";

import { useCallback, useSyncExternalStore } from "react";
import { liveVehicleStore } from "./live-vehicle-store";
import type { Vehicle } from "./schemas";

/** The live position of one vehicle. Re-renders only when that vehicle changes. */
export function useLiveVehicle(key: string | null): Vehicle | undefined {
  const subscribe = useCallback(
    (onChange: () => void) => (key ? liveVehicleStore.subscribeToVehicle(key, onChange) : () => {}),
    [key],
  );
  return useSyncExternalStore(
    subscribe,
    () => (key ? liveVehicleStore.get(key) : undefined),
    () => undefined,
  );
}

/** How many vehicles are tracked right now. */
export function useLiveVehicleCount(): number {
  return useSyncExternalStore(liveVehicleStore.subscribe, liveVehicleStore.getCount, () => 0);
}
