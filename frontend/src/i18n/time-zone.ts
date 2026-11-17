/**
 * Every time on screen is shown in the time zone the vehicles run in, not the viewer's.
 * A bus at 07:33 in Norway is "07:33" for everyone, which is also how timetables are written.
 * Timestamps themselves travel and are stored as UTC; this only affects how they are displayed.
 */
export const DISPLAY_TIME_ZONE = "Europe/Oslo";

/** "UTC+1" in winter, "UTC+2" in summer: computed for the given moment, so daylight saving is handled. */
export function utcOffsetLabel(at: Date): string {
  const parts = new Intl.DateTimeFormat("en", {
    timeZone: DISPLAY_TIME_ZONE,
    timeZoneName: "shortOffset",
  }).formatToParts(at);
  const offset = parts.find((part) => part.type === "timeZoneName")?.value ?? "GMT";
  return offset.replace("GMT", "UTC");
}
