"use client";

import { useSearchParams } from "next/navigation";
import { useCallback } from "react";
import { usePathname, useRouter } from "@/i18n/navigation";

const PARAM = "q";

/** The filter text lives in the URL (?q=Line:31) so a reload or a shared link keeps it. */
export function useVehicleFilter(): [string, (value: string) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const filter = searchParams.get(PARAM) ?? "";

  const setFilter = useCallback(
    (value: string) => {
      const next = new URLSearchParams(searchParams);
      if (value) next.set(PARAM, value);
      else next.delete(PARAM);
      const query = next.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [router, pathname, searchParams],
  );

  return [filter, setFilter];
}
