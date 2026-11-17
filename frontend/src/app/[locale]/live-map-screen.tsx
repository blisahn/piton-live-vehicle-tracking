"use client";

import { useTranslations } from "next-intl";
import { PanelRightClose, PanelRightOpen } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { usePanelRef } from "react-resizable-panels";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Drawer, DrawerContent } from "@/components/ui/drawer";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { useIsDesktop } from "@/shared/lib/use-media-query";
import { useSelectedVehicle } from "@/entities/vehicle";
import {
  LiveMap,
  VehicleFilter,
  useVehicleFilter,
  type RouteOverlay,
  type TrackOverlay,
} from "@/features/live-map";
import { VehicleAlerts } from "@/features/alerts";
import { VehiclePanel } from "@/features/vehicle-detail";
import { HistoryPanel } from "@/features/vehicle-history";
import { RouteSection } from "@/features/vehicle-route";

/**
 * The live map screen. Features do not know about each other; this is the one place that
 * connects them: the selected vehicle and the filter come from the URL, and the history
 * feature hands its track to the map through state held here.
 */
export function LiveMapScreen() {
  const t = useTranslations("vehicle");
  const [selectedKey, selectVehicle] = useSelectedVehicle();
  const [filter] = useVehicleFilter();
  const [track, setTrack] = useState<TrackOverlay | null>(null);
  const [route, setRoute] = useState<RouteOverlay | null>(null);

  // Drop the previous vehicle's overlays during render (React's "adjust state on prop change" pattern).
  const [overlaysFor, setOverlaysFor] = useState(selectedKey);
  if (overlaysFor !== selectedKey) {
    setOverlaysFor(selectedKey);
    setTrack(null);
    setRoute(null);
  }

  // The measured path follows the planned route where the vehicle was on it.
  // An approximate route is only straight lines between stops; snapping real positions to it would be wrong.
  const snapPath = useMemo(
    () => (route && !route.approximate ? [...route.passed, ...route.remaining] : null),
    [route],
  );

  const isDesktop = useIsDesktop();
  const [detailSize] = useState(readDetailSize);

  // Desktop: the panel stays mounted and slides open or closed, instead of appearing in one jump.
  // "hidden" is the user folding it away with the toggle; the vehicle stays selected meanwhile.
  const [hidden, setHidden] = useState(false);
  const panelOpen = selectedKey !== null && !hidden;
  const detailPanelRef = usePanelRef();
  const [animating, setAnimating] = useState(false);
  const animationTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Keep showing the last vehicle while the panel slides shut, so it does not empty out mid-animation.
  const [shownKey, setShownKey] = useState(selectedKey);
  if (selectedKey !== null && shownKey !== selectedKey) setShownKey(selectedKey);
  // A newly selected vehicle always opens the panel, even if it was folded away.
  const [hiddenFor, setHiddenFor] = useState(selectedKey);
  if (hiddenFor !== selectedKey) {
    setHiddenFor(selectedKey);
    if (hidden) setHidden(false);
  }

  useEffect(() => {
    const panel = detailPanelRef.current;
    if (!isDesktop || !panel) return;
    if (panelOpen === !panel.isCollapsed()) return;
    // The transition is only switched on for these programmatic moves; dragging the handle stays immediate.
    setAnimating(true);
    if (panelOpen) panel.expand();
    else panel.collapse();
    if (animationTimer.current) clearTimeout(animationTimer.current);
    animationTimer.current = setTimeout(() => setAnimating(false), PANEL_ANIMATION_MS);
  }, [panelOpen, isDesktop, detailPanelRef]);
  useEffect(() => () => {
    if (animationTimer.current) clearTimeout(animationTimer.current);
  }, []);

  const panelKey = selectedKey ?? shownKey;
  const detail = panelKey ? (
    <VehiclePanel vehicleKey={panelKey} onClose={() => selectVehicle(null)}>
      <VehicleAlerts vehicleKey={panelKey} />
      <RouteSection vehicleKey={panelKey} onRouteChange={setRoute} />
      <HistoryPanel vehicleKey={panelKey} onTrackChange={setTrack} snapPath={snapPath} />
    </VehiclePanel>
  ) : null;

  return (
    // One tree for every screen size, so the map is never re-created when the breakpoint changes.
    // md and up: a detail panel beside the map that can be dragged wider. Below: a bottom drawer (see below).
    <ResizablePanelGroup
      orientation="horizontal"
      className={cn(
        "min-h-0 flex-1",
        animating && "[&>[data-panel]]:transition-[flex-grow] [&>[data-panel]]:duration-300 [&>[data-panel]]:ease-out",
      )}
      onLayoutChanged={(layout) => {
        // A collapsed panel reports 0; remember only real widths.
        const size = layout[DETAIL_PANEL_ID];
        if (size !== undefined && size > 0) saveDetailSize(size);
      }}
    >
      <ResizablePanel id="map" minSize="30%">
        <section className="relative h-full min-h-0">
          {/* Phone: narrow enough to leave the zoom controls (top-right) free. */}
          <div className="absolute left-2 right-14 top-2 z-10 rounded-lg border bg-background/95 p-2 shadow-sm md:right-auto md:w-80">
            <VehicleFilter />
          </div>
          <LiveMap selectedKey={selectedKey} onSelect={selectVehicle} filter={filter} track={track} route={route} />
          {isDesktop && selectedKey ? (
            // Below the zoom controls, like a sidebar trigger: folds the panel away without losing the selection.
            <Button
              variant="outline"
              size="icon-sm"
              className="absolute right-2.5 top-[112px] z-10 bg-background shadow-sm"
              aria-label={panelOpen ? t("hidePanel") : t("showPanel")}
              aria-expanded={panelOpen}
              aria-controls={DETAIL_CONTENT_ID}
              onClick={() => setHidden((value) => !value)}
            >
              {panelOpen ? <PanelRightClose aria-hidden /> : <PanelRightOpen aria-hidden />}
            </Button>
          ) : null}
        </section>
      </ResizablePanel>

      {isDesktop ? (
        <>
          <ResizableHandle withHandle className={cn(!panelOpen && "hidden")} />
          <ResizablePanel
            id={DETAIL_PANEL_ID}
            panelRef={detailPanelRef}
            collapsible
            collapsedSize="0%"
            defaultSize={panelOpen ? `${detailSize}%` : "0%"}
            minSize={DETAIL_MIN_PX}
            maxSize="50%"
          >
            <div id={DETAIL_CONTENT_ID} className={cn("h-full min-h-0 overflow-hidden", panelOpen && "border-l")}>{detail}</div>
          </ResizablePanel>
        </>
      ) : null}

      {/*
        Phone: a non-modal drawer, so the map stays usable. Snap points: a peek showing the title,
        half the screen, almost all of it. Dragging it down past the peek closes it and deselects.
        Pointer dismissal is off, otherwise tapping another vehicle on the map would close it.
      */}
      <Drawer
        open={selectedKey !== null && !isDesktop}
        onOpenChange={(open) => {
          if (!open) selectVehicle(null);
        }}
        modal={false}
        disablePointerDismissal
        snapPoints={DRAWER_SNAP_POINTS}
        defaultSnapPoint={DRAWER_SNAP_POINTS[0]}
        showSwipeHandle
      >
        <DrawerContent aria-label={t("drawerLabel")}>
          <div className="flex h-[calc(100dvh-var(--drawer-snap-point-offset,0px))] min-h-0 flex-col">{detail}</div>
        </DrawerContent>
      </Drawer>
    </ResizablePanelGroup>
  );
}

const DRAWER_SNAP_POINTS = ["140px", 0.5, 0.92];
const DETAIL_PANEL_ID = "detail";
const DETAIL_CONTENT_ID = "vehicle-detail-panel";
const DETAIL_MIN_PX = 320;
const DETAIL_DEFAULT = 28;
const DETAIL_STORAGE_KEY = "live-map:detail-size";
const PANEL_ANIMATION_MS = 320;

/** The detail panel's last width in percent. Read only once the panel is about to show (after hydration). */
function readDetailSize(): number {
  try {
    const value = Number(window.localStorage.getItem(DETAIL_STORAGE_KEY));
    return value >= 15 && value <= 50 ? value : DETAIL_DEFAULT;
  } catch {
    return DETAIL_DEFAULT;
  }
}

function saveDetailSize(percent: number) {
  try {
    window.localStorage.setItem(DETAIL_STORAGE_KEY, String(Math.round(percent * 10) / 10));
  } catch {
    // Storage blocked: the size just is not remembered.
  }
}
