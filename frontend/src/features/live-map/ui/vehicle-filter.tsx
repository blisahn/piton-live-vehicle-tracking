"use client";

import { useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useState, useSyncExternalStore } from "react";
import { useLines } from "@/entities/transit";
import { liveVehicleStore } from "@/entities/vehicle";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useVehicleFilter } from "../model/use-vehicle-filter";
import { useScreenBoundsStore } from "../model/screen-bounds-store";
import { countVehicles } from "../model/vehicle-geojson";

const DEBOUNCE_MS = 250;

export function VehicleFilter() {
  const t = useTranslations("liveMap.filter");
  const inputId = useId();
  const [filter, setFilter] = useVehicleFilter();
  const lines = useLines();
  const screen = useScreenBoundsStore((state) => state.bounds);

  // What the user is typing; the URL only follows after a short pause.
  const [value, setValue] = useState(filter);
  const [seenFilter, setSeenFilter] = useState(filter);
  if (filter !== seenFilter) {
    // The URL changed (back button, shared link): adopt it.
    setSeenFilter(filter);
    setValue(filter);
  }

  // Push to the URL once typing pauses, so each key press does not rewrite it.
  useEffect(() => {
    if (value === filter) return;
    const timer = setTimeout(() => setFilter(value), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [value, filter, setFilter]);

  // Re-count only when the store changed or the filter changed.
  const version = useSyncExternalStore(liveVehicleStore.subscribe, liveVehicleStore.getVersion, () => 0);
  const total = liveVehicleStore.getCount();
  // `version` is not read inside, but it is what tells us the store content changed.
  const { matching, onScreen } = useMemo(
    () => countVehicles(liveVehicleStore.all(), filter, lines, screen),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [version, filter, lines, screen],
  );

  // Before the map reports what it shows, fall back to the plain total.
  let countText: string;
  if (!screen) countText = t("count", { shown: matching, total });
  else if (filter.trim() === "") countText = t("countOnScreen", { onScreen });
  else countText = t("countFiltered", { matching, onScreen });

  return (
    <div className="flex flex-col gap-1 md:gap-1.5">
      <label htmlFor={inputId} className="sr-only text-sm font-medium md:not-sr-only">
        {t("label")}
      </label>
      <div className="flex items-center gap-2">
        <Input
          id={inputId}
          type="search"
          value={value}
          placeholder={t("placeholder")}
          autoComplete="off"
          onChange={(event) => setValue(event.target.value)}
        />
        {value ? (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              setValue("");
              setFilter("");
            }}
          >
            {t("clear")}
          </Button>
        ) : null}
      </div>
      <p className="text-[11px] text-muted-foreground md:text-xs" aria-live="polite">
        {countText}
      </p>
    </div>
  );
}
