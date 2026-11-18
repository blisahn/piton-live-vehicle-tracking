/** Picks a readable unit for a byte count: 950 → 950 B, 35 500 → 34.7 kB, 3 800 000 → 3.6 MB. */
export function dataSize(bytes: number): { value: number; unit: "byte" | "kilobyte" | "megabyte" } {
  if (bytes < 1024) return { value: bytes, unit: "byte" };
  if (bytes < 1024 * 1024) return { value: bytes / 1024, unit: "kilobyte" };
  return { value: bytes / (1024 * 1024), unit: "megabyte" };
}
