/** [west, south, east, north] in degrees. */
export type Bounds = [number, number, number, number];

/** Below this zoom the map shows (almost) the whole country: subscribing to an area would save nothing. */
export const MIN_AREA_ZOOM = 8;
/** Each side grows by this share of the visible width/height, so small pans stay inside the subscribed area. */
export const VIEWPORT_PADDING = 0.5;

export function shouldSubscribe(zoom: number): boolean {
  return zoom >= MIN_AREA_ZOOM;
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/** Grows every side by `factor` times the width (west/east) or height (south/north), within lat/lon limits. */
export function paddedBounds([west, south, east, north]: Bounds, factor: number): Bounds {
  const dx = (east - west) * factor;
  const dy = (north - south) * factor;
  return [
    clamp(west - dx, -180, 180),
    clamp(south - dy, -90, 90),
    clamp(east + dx, -180, 180),
    clamp(north + dy, -90, 90),
  ];
}

/** True when `inner` lies completely inside `outer` (touching edges count). */
export function contains(outer: Bounds, inner: Bounds): boolean {
  return outer[0] <= inner[0] && outer[1] <= inner[1] && outer[2] >= inner[2] && outer[3] >= inner[3];
}
