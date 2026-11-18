import { describe, expect, it } from "vitest";
import { dataSize } from "./data-size";

describe("dataSize", () => {
  it("keeps small counts in bytes", () => {
    expect(dataSize(0)).toEqual({ value: 0, unit: "byte" });
    expect(dataSize(1023)).toEqual({ value: 1023, unit: "byte" });
  });

  it("switches to kilobytes and megabytes at 1024", () => {
    expect(dataSize(1024)).toEqual({ value: 1, unit: "kilobyte" });
    expect(dataSize(35_500).unit).toBe("kilobyte");
    expect(dataSize(3 * 1024 * 1024)).toEqual({ value: 3, unit: "megabyte" });
  });
});
