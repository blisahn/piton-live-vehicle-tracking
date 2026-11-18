import { describe, expect, it } from "vitest";
import { safeNextPath } from "./next-path";

describe("safeNextPath", () => {
  it("keeps same-origin relative paths", () => {
    expect(safeNextPath("/alerts?x=1")).toBe("/alerts?x=1");
    expect(safeNextPath("/tr/alerts")).toBe("/alerts");
    expect(safeNextPath("/en")).toBe("/");
  });
  it("falls back to the map for anything else", () => {
    for (const bad of [null, "", "alerts", "//evil.com", "https://evil.com", "/\\evil.com"]) {
      expect(safeNextPath(bad)).toBe("/");
    }
  });
});
