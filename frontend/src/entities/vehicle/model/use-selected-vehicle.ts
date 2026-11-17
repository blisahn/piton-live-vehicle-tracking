"use client";

import { useSearchParams } from "next/navigation";
import { useCallback } from "react";
import { usePathname, useRouter } from "@/i18n/navigation";

const PARAM = "vehicle";

/**
 * Which vehicle is selected lives in the URL (?vehicle=RUT:101234), not in component state:
 * a reload or a shared link opens the same vehicle, and the map, the detail panel and the
 * history list agree without knowing about each other.
 */
export function useSelectedVehicle(): [string | null, (key: string | null) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const selected = searchParams.get(PARAM);

  const select = useCallback(
    (key: string | null) => {
      const next = new URLSearchParams(searchParams);
      if (key) next.set(PARAM, key);
      else next.delete(PARAM);
      const query = next.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [router, pathname, searchParams],
  );

  return [selected, select];
}
