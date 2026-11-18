"use client";

import { useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useState, useSyncExternalStore } from "react";
import { useLines } from "@/entities/transit";
import { liveVehicleStore, useIsAreaSubscription } from "@/entities/vehicle";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useVehicleFilter } from "../model/use-vehicle-filter";
import { countMatches } from "../model/vehicle-geojson";

const DEBOUNCE_MS = 250;

export function VehicleFilter() {
  const t = useTranslations("liveMap.filter");
  const inputId = useId();
  const [filter, setFilter] = useVehicleFilter();
  const lines = useLines();
  const inArea = useIsAreaSubscription();

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
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const shown = useMemo(() => countMatches(liveVehicleStore.all(), filter, lines), [version, filter, lines]);

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
        {inArea ? t("countInArea", { total }) : t("count", { shown, total })}
      </p>
    </div>
  );
}
