"use client";

import { useSearchParams } from "next/navigation";
import { useCallback } from "react";
import { usePathname, useRouter } from "@/i18n/navigation";
import type { AlertsFilter } from "../api/alerts";

type Patch = Partial<AlertsFilter>;

/** The filters live in the URL (?line=…&stop=…&inactive=1), so a reload or a shared link keeps them. */
export function useAlertsFilter(): [AlertsFilter, (patch: Patch) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const filter: AlertsFilter = {
    lineId: searchParams.get("line") || null,
    stopId: searchParams.get("stop") || null,
    includeInactive: searchParams.get("inactive") === "1",
  };

  const update = useCallback(
    (patch: Patch) => {
      const next = new URLSearchParams(searchParams);
      const set = (key: string, value: string | null) => (value ? next.set(key, value) : next.delete(key));
      if ("lineId" in patch) set("line", patch.lineId ?? null);
      if ("stopId" in patch) set("stop", patch.stopId ?? null);
      if ("includeInactive" in patch) set("inactive", patch.includeInactive ? "1" : null);
      const query = next.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [router, pathname, searchParams],
  );

  return [filter, update];
}
