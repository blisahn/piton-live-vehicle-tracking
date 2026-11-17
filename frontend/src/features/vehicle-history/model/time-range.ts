// Presets are minutes back from now; the backend keeps 3 hours, so 180 is the maximum.
export const RANGE_PRESETS = [15, 30, 60, 180] as const;
export type RangeMinutes = (typeof RANGE_PRESETS)[number];
export const DEFAULT_RANGE: RangeMinutes = 30;

export type TimeRange = { from: string; to: string };

/** Computed once per user choice (not per render), otherwise the query key would change forever. */
export function computeRange(minutes: RangeMinutes, now: Date = new Date()): TimeRange {
  return {
    from: new Date(now.getTime() - minutes * 60_000).toISOString(),
    to: now.toISOString(),
  };
}

export function isRangeMinutes(value: number): value is RangeMinutes {
  return RANGE_PRESETS.some((preset) => preset === value);
}
