import { describe, expect, it } from "vitest";
import { classifyDelay, matchProgressStop, pickStopTimes, type ProgressStopLike } from "./progress";

const stop = (over: Partial<ProgressStopLike> = {}): ProgressStopLike => ({
  sequence: 1,
  stopId: "A",
  aimedArrival: null,
  expectedArrival: null,
  aimedDeparture: null,
  expectedDeparture: null,
  skipped: false,
  ...over,
});

describe("classifyDelay", () => {
  it("canceled wins over any delay", () => {
    expect(classifyDelay(600, true)).toEqual({ kind: "canceled" });
  });
  it("has no data without a delay", () => {
    expect(classifyDelay(null, false)).toEqual({ kind: "none" });
  });
  it("is on time under a minute either way", () => {
    expect(classifyDelay(0, false).kind).toBe("onTime");
    expect(classifyDelay(59, false).kind).toBe("onTime");
    expect(classifyDelay(-59, false).kind).toBe("onTime");
  });
  it("is late from 60 s and rounds to whole minutes", () => {
    expect(classifyDelay(60, false)).toEqual({ kind: "late", minutes: 1, severity: "warning" });
    expect(classifyDelay(90, false)).toEqual({ kind: "late", minutes: 2, severity: "warning" });
    expect(classifyDelay(149, false)).toEqual({ kind: "late", minutes: 2, severity: "warning" });
  });
  it("is critical from 5 minutes", () => {
    expect(classifyDelay(299, false)).toMatchObject({ severity: "warning" });
    expect(classifyDelay(300, false)).toEqual({ kind: "late", minutes: 5, severity: "critical" });
  });
  it("is early from -60 s", () => {
    expect(classifyDelay(-60, false)).toEqual({ kind: "early", minutes: 1 });
    expect(classifyDelay(-200, false)).toEqual({ kind: "early", minutes: 3 });
  });
});

describe("pickStopTimes", () => {
  it("returns null without any planned time", () => {
    expect(pickStopTimes(stop())).toBeNull();
  });
  it("prefers arrival", () => {
    const times = pickStopTimes(
      stop({
        aimedArrival: "2026-01-01T13:13:00Z",
        expectedArrival: "2026-01-01T13:17:00Z",
        aimedDeparture: "2026-01-01T13:14:00Z",
      }),
    );
    expect(times).toEqual({ planned: "2026-01-01T13:13:00Z", expected: "2026-01-01T13:17:00Z", shiftSeconds: 240 });
  });
  it("falls back to departure when there is no arrival", () => {
    const times = pickStopTimes(
      stop({ aimedDeparture: "2026-01-01T13:00:00Z", expectedDeparture: "2026-01-01T12:58:00Z" }),
    );
    expect(times).toEqual({ planned: "2026-01-01T13:00:00Z", expected: "2026-01-01T12:58:00Z", shiftSeconds: -120 });
  });
  it("hides the expected time when it differs by under a minute", () => {
    const times = pickStopTimes(stop({ aimedArrival: "2026-01-01T13:00:00Z", expectedArrival: "2026-01-01T13:00:59Z" }));
    expect(times?.expected).toBeNull();
    const exact = pickStopTimes(stop({ aimedArrival: "2026-01-01T13:00:00Z", expectedArrival: "2026-01-01T13:01:00Z" }));
    expect(exact?.expected).toBe("2026-01-01T13:01:00Z");
  });
  it("shows only the planned time without an expected one", () => {
    expect(pickStopTimes(stop({ aimedArrival: "2026-01-01T13:00:00Z" }))?.expected).toBeNull();
  });
});

describe("matchProgressStop", () => {
  const stops = [stop({ stopId: "A", sequence: 1 }), stop({ stopId: "B", sequence: 2 })];
  it("matches by stop id", () => {
    expect(matchProgressStop(stops, "B", 0)?.stopId).toBe("B");
  });
  it("falls back to sequence", () => {
    expect(matchProgressStop(stops, "X", 1)?.stopId).toBe("B");
  });
  it("returns undefined when nothing matches", () => {
    expect(matchProgressStop(stops, "X", 5)).toBeUndefined();
  });
});
