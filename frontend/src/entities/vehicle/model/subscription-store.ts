import { create } from "zustand";

/** [west, south, east, north] in degrees. */
export type Bbox = [number, number, number, number];

/** What the server is asked to send. Matching rule (server side): key in keys, else routeId in lines, else inside bbox, else all. */
export type Subscription = { bbox: Bbox | null; lines: string[]; keys: string[] };

type SubscriptionState = Subscription & {
  /**
   * True while the filter text matches no line (e.g. a vehicle number search). The client-side filter then needs
   * every vehicle, so the area is ignored without forgetting it: it applies again once the filter changes.
   */
  viewportSuspended: boolean;
  setViewport: (bbox: Bbox | null) => void;
  setLines: (lines: string[]) => void;
  setKeys: (keys: string[]) => void;
  setViewportSuspended: (suspended: boolean) => void;
};

export const useSubscriptionStore = create<SubscriptionState>((set) => ({
  bbox: null,
  lines: [],
  keys: [],
  viewportSuspended: false,
  setViewport: (bbox) => set({ bbox }),
  setLines: (lines) => set({ lines }),
  setKeys: (keys) => set({ keys }),
  setViewportSuspended: (viewportSuspended) => set({ viewportSuspended }),
}));

/** The one place that decides what is actually sent: the area is dropped while the filter overrides it. */
export function effectiveSubscription(state: SubscriptionState): Subscription {
  return { bbox: state.viewportSuspended ? null : state.bbox, lines: state.lines, keys: state.keys };
}

/** True when the vehicles on screen are only those of the visible area (so counts mean "in this area"). */
export function useIsAreaSubscription(): boolean {
  return useSubscriptionStore((s) => s.bbox !== null && !s.viewportSuspended && s.lines.length === 0);
}

/** Order-insensitive identity, to skip sending a subscription the server already has. */
export function subscriptionKey(subscription: Subscription): string {
  return JSON.stringify([subscription.bbox, [...subscription.lines].sort(), [...subscription.keys].sort()]);
}
