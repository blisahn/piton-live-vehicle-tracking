"use client";

import { useSyncExternalStore } from "react";

/**
 * Whether a CSS media query matches. The server (and the hydration pass) answer `false`, then the
 * real value follows, so there is no hydration mismatch.
 */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const list = window.matchMedia(query);
      list.addEventListener("change", onChange);
      return () => list.removeEventListener("change", onChange);
    },
    () => window.matchMedia(query).matches,
    () => false,
  );
}

/** Tailwind's `md` breakpoint: the layout with the panel beside the map. */
export const useIsDesktop = () => useMediaQuery("(min-width: 768px)");
