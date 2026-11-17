// The public surface of the transit entity: lines and stops as the backend knows them.
export { useLines, lineSchema, type Line } from "./api/lines";
export { useStops, stopSchema, type Stop } from "./api/stops";
export { lineLabel, lineMatches, stopLabel } from "./model/format";
