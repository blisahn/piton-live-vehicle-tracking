import type { Line } from "../api/lines";
import type { Stop } from "../api/stops";

/**
 * "M1 · Flekkerøy - Sørlandsparken"; whatever part is missing is left out; the raw id when the line is unknown.
 * Some operators repeat the code as the name (Flytoget: "FLY1" / "FLY1"), which is shown once.
 */
export function lineLabel(line: Line | undefined, fallbackId: string | null): string | null {
  if (line) {
    const code = line.publicCode?.trim() || null;
    const name = line.name?.trim() || null;
    const sameText = code !== null && name !== null && code.toLowerCase() === name.toLowerCase();
    const parts = (sameText ? [code] : [code, name]).filter((part): part is string => Boolean(part));
    if (parts.length > 0) return parts.join(" · ");
  }
  return fallbackId;
}

export function stopLabel(stop: Stop | undefined, fallbackId: string | null): string | null {
  return stop?.name ?? fallbackId;
}

/** Case-insensitive "contains" on the public code and the name. */
export function lineMatches(line: Line | undefined, needle: string): boolean {
  if (!line) return false;
  const text = needle.toLowerCase();
  return (
    (line.publicCode?.toLowerCase().includes(text) ?? false) || (line.name?.toLowerCase().includes(text) ?? false)
  );
}
