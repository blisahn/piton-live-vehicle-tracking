import { create } from "zustand";
import type { Bounds } from "./viewport";

/**
 * What the map currently shows, so the counter can say how many vehicles are on screen.
 * Not the subscription area: that one is padded by half a screen on each side and only follows
 * larger moves, so counting it would overstate what the user sees.
 */
export const useScreenBoundsStore = create<{ bounds: Bounds | null; setBounds: (bounds: Bounds | null) => void }>(
  (set) => ({ bounds: null, setBounds: (bounds) => set({ bounds }) }),
);
