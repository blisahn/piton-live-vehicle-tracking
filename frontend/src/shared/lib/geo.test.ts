import { describe, expect, it } from "vitest";
import { distanceMeters, locateStopsAlongPath, projectOntoPath, snapTrackToPath, splitPathAt, type GeoPoint } from "./geo";

// 0.009 degrees of latitude is about 1 km.
const origin: GeoPoint = { lat: 59, lon: 10 };
const oneKmNorth: GeoPoint = { lat: 59.008993, lon: 10 };

describe("distanceMeters", () => {
  it("measures about one kilometre for a known pair", () => {
    expect(distanceMeters(origin, oneKmNorth)).toBeGreaterThan(990);
    expect(distanceMeters(origin, oneKmNorth)).toBeLessThan(1010);
  });
});

describe("projectOntoPath", () => {
  const path: GeoPoint[] = [origin, { lat: 59, lon: 10.02 }];

  it("projects a point beside the middle onto the middle", () => {
    const result = projectOntoPath({ lat: 59.0005, lon: 10.01 }, path);
    expect(result.fraction).toBeCloseTo(0.5, 2);
    expect(result.point.lon).toBeCloseTo(10.01, 5);
    expect(result.distanceMeters).toBeGreaterThan(50);
    expect(result.distanceMeters).toBeLessThan(60);
    expect(result.alongMeters).toBeCloseTo(distanceMeters(path[0], path[1]) / 2, -1);
  });

  it("clamps a point before the start to the start", () => {
    const result = projectOntoPath({ lat: 59, lon: 9.99 }, path);
    expect(result.fraction).toBe(0);
    expect(result.alongMeters).toBe(0);
  });

  it("clamps a point after the end to the end", () => {
    const result = projectOntoPath({ lat: 59, lon: 10.05 }, path);
    expect(result.fraction).toBe(1);
    expect(result.alongMeters).toBeCloseTo(distanceMeters(path[0], path[1]), 3);
  });
});

// East for about 700 m, then north for about 1 km.
const corner: GeoPoint = { lat: 59, lon: 10.012 };
const lPath: GeoPoint[] = [origin, corner, { lat: 59.009, lon: 10.012 }];

describe("splitPathAt", () => {
  it("keeps the corner in the passed part when the vehicle is beyond it", () => {
    const result = splitPathAt(lPath, { lat: 59.004, lon: 10.012 });
    expect(result.passed).toContainEqual(corner);
    expect(result.remaining).not.toContainEqual(corner);
    expect(result.passed.at(-1)).toEqual(result.remaining[0]);
    expect(result.offRouteMeters).toBeLessThan(1);
  });
});

describe("snapTrackToPath", () => {
  it("follows the corner between two on-route points on either side of it", () => {
    const before: GeoPoint = { lat: 59, lon: 10.008 };
    const after: GeoPoint = { lat: 59.004, lon: 10.012 };
    const drawn = snapTrackToPath([before, after], lPath);
    expect(drawn).toContainEqual(corner);
  });

  it("draws a straight line when a point is 200 m off the path", () => {
    const before: GeoPoint = { lat: 59, lon: 10.008 };
    // About 200 m east of the northbound leg.
    const away: GeoPoint = { lat: 59.004, lon: 10.0155 };
    expect(snapTrackToPath([before, away], lPath)).toEqual([before, away]);
  });

  it("matches stops to the right pass on a route that uses the same road twice", () => {
    // Out along a straight road, turn at B and come back: A -> B -> A.
    const a = { lat: 60, lon: 10 };
    const b = { lat: 60, lon: 10.02 };
    const outAndBack = [a, b, a];
    const halfway = { lat: 60, lon: 10.01 };

    // The trip serves the halfway stop on the way out, the turning point, then the halfway stop again.
    const [outbound, turn, inbound] = locateStopsAlongPath([halfway, b, halfway], outAndBack);

    expect(outbound.segmentIndex).toBe(0);
    expect(inbound.segmentIndex).toBe(1);
    expect(outbound.alongMeters).toBeLessThan(turn.alongMeters);
    expect(inbound.alongMeters).toBeGreaterThan(turn.alongMeters);
  });

  it("projects only inside the given window", () => {
    const a = { lat: 60, lon: 10 };
    const b = { lat: 60, lon: 10.02 };
    const onTheWayBack = projectOntoPath({ lat: 60, lon: 10.01 }, [a, b, a], { from: 1, to: 1 });

    expect(onTheWayBack.segmentIndex).toBe(1);
  });
});
