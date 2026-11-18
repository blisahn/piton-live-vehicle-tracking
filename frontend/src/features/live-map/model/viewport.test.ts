import { describe, expect, it } from "vitest";
import { contains, isInside, MIN_AREA_ZOOM, paddedBounds, shouldSubscribe, type Bounds } from "./viewport";

describe("paddedBounds", () => {
  it("grows each side by the factor times the width and height", () => {
    expect(paddedBounds([10, 59, 12, 60], 0.5)).toEqual([9, 58.5, 13, 60.5]);
  });

  it("does not change anything with factor 0", () => {
    expect(paddedBounds([10, 59, 12, 60], 0)).toEqual([10, 59, 12, 60]);
  });

  it("stays within latitude and longitude limits", () => {
    expect(paddedBounds([-170, -80, 170, 80], 1)).toEqual([-180, -90, 180, 90]);
  });
});

describe("contains", () => {
  const outer: Bounds = [9, 58, 13, 61];

  it("is true for a box inside, and for an identical one", () => {
    expect(contains(outer, [10, 59, 12, 60])).toBe(true);
    expect(contains(outer, outer)).toBe(true);
  });

  it("is false when any side sticks out", () => {
    expect(contains(outer, [8.9, 59, 12, 60])).toBe(false);
    expect(contains(outer, [10, 57.9, 12, 60])).toBe(false);
    expect(contains(outer, [10, 59, 13.1, 60])).toBe(false);
    expect(contains(outer, [10, 59, 12, 61.1])).toBe(false);
  });

  it("is false for a bigger box", () => {
    expect(contains([10, 59, 12, 60], outer)).toBe(false);
  });
});

describe("shouldSubscribe", () => {
  it("only subscribes to an area from the threshold zoom up", () => {
    expect(shouldSubscribe(MIN_AREA_ZOOM - 0.01)).toBe(false);
    expect(shouldSubscribe(MIN_AREA_ZOOM)).toBe(true);
    expect(shouldSubscribe(12)).toBe(true);
  });
});

describe("isInside", () => {
  const oslo: [number, number, number, number] = [10.6, 59.8, 10.9, 60.0];
  it("counts positions inside and on the edge, not outside", () => {
    expect(isInside(oslo, 10.75, 59.91)).toBe(true);
    expect(isInside(oslo, 10.6, 60.0)).toBe(true);
    expect(isInside(oslo, 10.5, 59.91)).toBe(false);
    expect(isInside(oslo, 10.75, 60.1)).toBe(false);
  });
});
