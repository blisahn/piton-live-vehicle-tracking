// Pure geometry helpers for drawing and comparing routes. No React, no map library.

export type GeoPoint = { lat: number; lon: number };

const EARTH_RADIUS_METERS = 6_371_000;
const toRadians = (degrees: number) => (degrees * Math.PI) / 180;

/** Great-circle distance (haversine). */
export function distanceMeters(a: GeoPoint, b: GeoPoint): number {
  const dLat = toRadians(b.lat - a.lat);
  const dLon = toRadians(b.lon - a.lon);
  const h =
    Math.sin(dLat / 2) ** 2 + Math.cos(toRadians(a.lat)) * Math.cos(toRadians(b.lat)) * Math.sin(dLon / 2) ** 2;
  return 2 * EARTH_RADIUS_METERS * Math.asin(Math.sqrt(h));
}

export type PathProjection = {
  /** The path segment (from path[i] to path[i + 1]) the closest point lies on. */
  segmentIndex: number;
  /** 0 at the segment's start, 1 at its end. */
  fraction: number;
  point: GeoPoint;
  /** How far the input point is from the path. */
  distanceMeters: number;
  /** Distance travelled along the path from its start to the projected point. */
  alongMeters: number;
};

/**
 * A stretch of a path: segments `from` to `to` (both included), and optionally nothing that lies
 * less than `minAlongMeters` from the path start.
 */
export type SegmentWindow = { from: number; to: number; minAlongMeters?: number };

/**
 * The closest point on a polyline. Each segment is flattened around the point's latitude
 * (x scaled by cos(lat)); that is accurate enough at city scale and keeps the math simple.
 * The path needs at least one point.
 *
 * `window` limits the search to part of the path. A route that uses the same road twice
 * (out and back, or a loop) has two equally close candidates; the window says which one is meant.
 */
export function projectOntoPath(point: GeoPoint, path: GeoPoint[], window?: SegmentWindow): PathProjection {
  if (path.length === 1) {
    const only = path[0];
    return { segmentIndex: 0, fraction: 0, point: only, distanceMeters: distanceMeters(point, only), alongMeters: 0 };
  }
  const lonScale = Math.cos(toRadians(point.lat));
  const first = Math.max(0, window?.from ?? 0);
  const last = Math.min(path.length - 2, window?.to ?? path.length - 2);
  let best: PathProjection | null = null;
  let walked = 0;

  for (let i = 0; i <= last; i++) {
    const start = path[i];
    const end = path[i + 1];
    const segmentLength = distanceMeters(start, end);
    // Local flat coordinates relative to the segment start, in degrees.
    const dx = (end.lon - start.lon) * lonScale;
    const dy = end.lat - start.lat;
    const px = (point.lon - start.lon) * lonScale;
    const py = point.lat - start.lat;
    const lengthSquared = dx * dx + dy * dy;
    const raw = lengthSquared === 0 ? 0 : (px * dx + py * dy) / lengthSquared;
    const fraction = Math.min(1, Math.max(0, raw));
    const projected: GeoPoint = {
      lat: start.lat + (end.lat - start.lat) * fraction,
      lon: start.lon + (end.lon - start.lon) * fraction,
    };
    const distance = distanceMeters(point, projected);
    const along = walked + segmentLength * fraction;
    // Segments before the window still count towards alongMeters, they are just not candidates.
    const inWindow = i >= first && along >= (window?.minAlongMeters ?? -Infinity);
    if (inWindow && (best === null || distance < best.distanceMeters)) {
      best = { segmentIndex: i, fraction, point: projected, distanceMeters: distance, alongMeters: along };
    }
    walked += segmentLength;
  }
  // An empty or inverted window falls back to its first segment.
  return best ?? projectOntoPath(point, path, { from: Math.min(first, path.length - 2), to: Math.min(first, path.length - 2) });
}

/**
 * Where each stop lies on the path, in order. Each stop is searched for only from the previous
 * stop's segment onwards, so on a route that passes the same place twice the stops are matched
 * to the right pass instead of all to the nearest one.
 */
export function locateStopsAlongPath(stops: GeoPoint[], path: GeoPoint[]): PathProjection[] {
  if (path.length === 0) return [];
  let previous: PathProjection | null = null;
  return stops.map((stop) => {
    // Never behind the previous stop. Checking the distance along the path, not just the segment,
    // matters at a terminus: the turning point is the end of one segment and the start of the next.
    const projection: PathProjection = projectOntoPath(stop, path, {
      from: previous?.segmentIndex ?? 0,
      to: path.length - 2,
      minAlongMeters: previous?.alongMeters,
    });
    previous = projection;
    return projection;
  });
}

export type SplitPath = {
  /** From the path start to the projected point. */
  passed: GeoPoint[];
  /** From the projected point to the path end. */
  remaining: GeoPoint[];
  /** How far the position is from the path. */
  offRouteMeters: number;
};

/** Cuts the path at the point closest to `position` (within `window`, if given); both halves include that point. */
export function splitPathAt(path: GeoPoint[], position: GeoPoint, window?: SegmentWindow): SplitPath {
  if (path.length === 0) return { passed: [], remaining: [], offRouteMeters: 0 };
  const projection = projectOntoPath(position, path, window);
  const cut = projection.point;
  return {
    passed: [...path.slice(0, projection.segmentIndex + 1), cut],
    remaining: [cut, ...path.slice(projection.segmentIndex + 1)],
    offRouteMeters: projection.distanceMeters,
  };
}

/**
 * The line to draw for a measured track (oldest first). Two neighbouring measurements that are
 * both within `maxOffsetMeters` of the path are joined by the stretch of the PATH between their
 * projections, so the line follows the road. Otherwise, or if the second one projects behind the
 * first (against the direction of travel), they are joined by a straight line.
 */
export function snapTrackToPath(track: GeoPoint[], path: GeoPoint[], maxOffsetMeters = 50): GeoPoint[] {
  if (track.length === 0 || path.length < 2) return track;
  const projections = track.map((point) => projectOntoPath(point, path));
  const onPath = projections.map((p) => p.distanceMeters <= maxOffsetMeters);
  const drawn: GeoPoint[] = [track[0]];

  for (let i = 1; i < track.length; i++) {
    const from = projections[i - 1];
    const to = projections[i];
    if (onPath[i - 1] && onPath[i] && to.alongMeters >= from.alongMeters) {
      // Path vertices strictly between the two projections.
      drawn.push(from.point, ...path.slice(from.segmentIndex + 1, to.segmentIndex + 1), to.point);
    }
    drawn.push(track[i]);
  }
  return drawn;
}
