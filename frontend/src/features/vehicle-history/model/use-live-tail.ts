"use client";

import { useState } from "react";
import { useLiveVehicle, type Vehicle } from "@/entities/vehicle";

/** Enough for hours of a vehicle that reports every few seconds, without growing forever. */
const MAX_TAIL = 2000;
const NONE: Vehicle[] = [];

/**
 * The positions a vehicle has reported live since `since`, newest first.
 *
 * The history query is a snapshot of the moment it ran. Without this the list and the
 * drawn path would stop at that moment while the vehicle keeps moving on the map.
 */
export function useLiveTail(vehicleKey: string, since: string): Vehicle[] {
  const live = useLiveVehicle(vehicleKey);

  // The tail belongs to one vehicle and one query; a new vehicle or a refreshed range starts an empty one.
  const scope = `${vehicleKey}|${since}`;
  const [tail, setTail] = useState<{ scope: string; items: Vehicle[] }>({ scope, items: NONE });
  const items = tail.scope === scope ? tail.items : NONE;

  // Adjusting state during render (instead of in an effect) is React's pattern for state derived
  // from a changing input; the condition is false on the re-render it triggers, so it settles at once.
  const newest = items[0]?.recordedAt ?? since;
  if (live && Date.parse(live.recordedAt) > Date.parse(newest)) {
    setTail({ scope, items: [live, ...items].slice(0, MAX_TAIL) });
  }

  return items;
}
