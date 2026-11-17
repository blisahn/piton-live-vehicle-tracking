/** Pure helpers for the trip's realtime schedule. */

export type DelayInfo =
  | { kind: "canceled" }
  | { kind: "none" }
  | { kind: "onTime" }
  | { kind: "late"; minutes: number; severity: "warning" | "critical" }
  | { kind: "early"; minutes: number };

const ON_TIME_LIMIT_SECONDS = 60;
const CRITICAL_SECONDS = 5 * 60;

/** Turns the delay at the next stop into what the badge says. Minutes are rounded to whole ones. */
export function classifyDelay(delaySeconds: number | null, canceled: boolean): DelayInfo {
  if (canceled) return { kind: "canceled" };
  if (delaySeconds === null) return { kind: "none" };
  if (Math.abs(delaySeconds) < ON_TIME_LIMIT_SECONDS) return { kind: "onTime" };
  const minutes = Math.round(Math.abs(delaySeconds) / 60);
  if (delaySeconds > 0) {
    return { kind: "late", minutes, severity: delaySeconds >= CRITICAL_SECONDS ? "critical" : "warning" };
  }
  return { kind: "early", minutes };
}

export type ProgressStopLike = {
  sequence: number;
  stopId: string;
  aimedArrival: string | null;
  expectedArrival: string | null;
  aimedDeparture: string | null;
  expectedDeparture: string | null;
  skipped: boolean;
};

export type StopTimes = {
  planned: string;
  /** Only set when the expected time differs from the planned one by a minute or more. */
  expected: string | null;
  /** Positive = later than planned. */
  shiftSeconds: number;
};

/** Planned and expected time of a stop: arrival, or departure when there is no arrival time. */
export function pickStopTimes(stop: ProgressStopLike): StopTimes | null {
  const useArrival = stop.aimedArrival !== null;
  const planned = useArrival ? stop.aimedArrival : stop.aimedDeparture;
  if (planned === null) return null;
  const expected = useArrival ? stop.expectedArrival : stop.expectedDeparture;
  if (expected === null) return { planned, expected: null, shiftSeconds: 0 };
  const shiftSeconds = Math.round((Date.parse(expected) - Date.parse(planned)) / 1000);
  if (Number.isNaN(shiftSeconds) || Math.abs(shiftSeconds) < ON_TIME_LIMIT_SECONDS) {
    return { planned, expected: null, shiftSeconds: Number.isNaN(shiftSeconds) ? 0 : shiftSeconds };
  }
  return { planned, expected, shiftSeconds };
}

/** Finds the progress entry for a route stop: by stop id, else by position in the trip (1-based sequence). */
export function matchProgressStop<T extends ProgressStopLike>(
  stops: readonly T[],
  stopId: string,
  routeIndex: number,
): T | undefined {
  return stops.find((s) => s.stopId === stopId) ?? stops.find((s) => s.sequence === routeIndex + 1);
}
