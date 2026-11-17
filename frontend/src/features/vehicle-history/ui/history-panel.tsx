"use client";

import { useEffect, useMemo, useState } from "react";
import { useFormatter, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { snapTrackToPath, type GeoPoint } from "@/shared/lib/geo";
import { EmptyState, ErrorState, LoadingState } from "@/shared/ui/state-views";
import { useVehicleHistory } from "../api/history";
import {
  computeRange,
  DEFAULT_RANGE,
  isRangeMinutes,
  RANGE_PRESETS,
  type RangeMinutes,
} from "../model/time-range";
import { useLiveTail } from "../model/use-live-tail";

/** `measurements` are the positions the vehicle reported (oldest first); `path` is the line drawn through them. */
export type HistoryTrack = { path: GeoPoint[]; measurements: GeoPoint[] };

type HistoryPanelProps = {
  vehicleKey: string;
  /** Called with the track while "show on map" is on, null when off. */
  onTrackChange: (track: HistoryTrack | null) => void;
  /** The planned route; where the vehicle was on it, the drawn line follows it instead of cutting corners. */
  snapPath?: GeoPoint[] | null;
};

type StatusKey = "0" | "1" | "2";
const STATUS_KEYS: Record<number, StatusKey> = { 0: "0", 1: "1", 2: "2" };

export function HistoryPanel({ vehicleKey, onTrackChange, snapPath = null }: HistoryPanelProps) {
  const t = useTranslations("history");
  const format = useFormatter();
  const [minutes, setMinutes] = useState<RangeMinutes>(DEFAULT_RANGE);
  const [range, setRange] = useState(() => computeRange(DEFAULT_RANGE));
  const [showOnMap, setShowOnMap] = useState(false);
  // Reset the toggle during render when the vehicle changes (React's "adjust state on prop change" pattern).
  const [shownFor, setShownFor] = useState(vehicleKey);
  if (shownFor !== vehicleKey) {
    setShownFor(vehicleKey);
    setShowOnMap(false);
  }

  const query = useVehicleHistory(vehicleKey, range);

  // What the vehicle reported after the query ran. Everything in it is newer than the stored pages.
  const liveTail = useLiveTail(vehicleKey, range.to);

  // Newest first: the live tail, then the stored pages exactly as the server returned them.
  const items = useMemo(
    () => [...liveTail, ...(query.data?.pages.flatMap((page) => page.items) ?? [])],
    [liveTail, query.data],
  );

  // Depends on `items` (stable until data changes), so it cannot loop.
  useEffect(() => {
    if (!showOnMap) return;
    const measurements = items.map(({ lat, lon }) => ({ lat, lon })).reverse();
    const path = snapPath && snapPath.length >= 2 ? snapTrackToPath(measurements, snapPath) : measurements;
    onTrackChange({ path, measurements });
  }, [showOnMap, items, snapPath, onTrackChange]);

  // Clear the track when the vehicle changes, the toggle goes off, or the panel unmounts.
  useEffect(() => {
    if (!showOnMap) return;
    return () => onTrackChange(null);
  }, [showOnMap, vehicleKey, onTrackChange]);

  function chooseRange(value: string) {
    const next = Number(value);
    if (!isRangeMinutes(next)) return;
    setMinutes(next);
    setRange(computeRange(next));
  }

  return (
    <section aria-labelledby="history-title" className="border-t p-4">
      <h3 id="history-title" className="text-sm font-semibold">
        {t("title")}
      </h3>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <label className="sr-only" htmlFor="history-range">
          {t("range.label")}
        </label>
        <select
          id="history-range"
          value={minutes}
          onChange={(event) => chooseRange(event.target.value)}
          className="h-8 rounded-lg border border-input bg-background px-2 text-sm"
        >
          {RANGE_PRESETS.map((preset) => (
            <option key={preset} value={preset}>
              {t(`range.${preset}`)}
            </option>
          ))}
        </select>
        <Button variant="outline" size="sm" onClick={() => setRange(computeRange(minutes))}>
          {t("refresh")}
        </Button>
        <Button
          variant={showOnMap ? "default" : "outline"}
          size="sm"
          aria-pressed={showOnMap}
          onClick={() => setShowOnMap((on) => !on)}
        >
          {showOnMap ? t("hideFromMap") : t("showOnMap")}
        </Button>
      </div>

      {showOnMap ? <p className="mt-2 text-xs text-muted-foreground">{t("pathHint")}</p> : null}

      {query.isPending ? (
        <LoadingState />
      ) : query.isError ? (
        <ErrorState message={query.error.message} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState title={t("empty")} hint={t("emptyHint")} />
      ) : (
        <div className="mt-3">
          <div className="max-h-64 overflow-auto rounded-lg border">
            <table className="w-full text-left text-xs">
              <thead className="sticky top-0 bg-muted">
                <tr>
                  <th scope="col" className="px-2 py-1.5 font-medium">{t("columns.time")}</th>
                  <th scope="col" className="px-2 py-1.5 font-medium">{t("columns.position")}</th>
                  <th scope="col" className="px-2 py-1.5 font-medium">{t("columns.status")}</th>
                  <th scope="col" className="px-2 py-1.5 font-medium">{t("columns.speed")}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => {
                  const statusKey = item.status === null ? undefined : STATUS_KEYS[item.status];
                  return (
                    <tr key={item.recordedAt} className="border-t">
                      <td className="px-2 py-1 whitespace-nowrap">
                        {format.dateTime(new Date(item.recordedAt), { timeStyle: "medium" })}
                      </td>
                      <td className="px-2 py-1 whitespace-nowrap tabular-nums">
                        {item.lat.toFixed(5)}, {item.lon.toFixed(5)}
                      </td>
                      <td className="px-2 py-1">{statusKey ? t(`status.${statusKey}`) : t("none")}</td>
                      <td className="px-2 py-1 whitespace-nowrap">
                        {item.speed === null
                          ? t("none")
                          : t("speedValue", { value: Math.round(item.speed * 3.6) })}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {query.hasNextPage ? (
            <Button
              variant="outline"
              size="sm"
              className="mt-2"
              disabled={query.isFetchingNextPage}
              onClick={() => void query.fetchNextPage()}
            >
              {query.isFetchingNextPage ? t("loadingMore") : t("loadMore")}
            </Button>
          ) : null}
        </div>
      )}
    </section>
  );
}
