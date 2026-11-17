"use client";

import { useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { lineLabel } from "@/entities/transit";
import { useLiveVehicle } from "@/entities/vehicle";
import { ApiError } from "@/shared/api/http";
import { locateStopsAlongPath, projectOntoPath, splitPathAt, type GeoPoint, type SegmentWindow } from "@/shared/lib/geo";
import { EmptyState, ErrorState, LoadingState } from "@/shared/ui/state-views";
import { useTripRoute } from "../api/route";

/** Same shape as the map's overlay type; features cannot import each other, so the page connects them. */
export type RouteOverlay = {
  passed: GeoPoint[];
  remaining: GeoPoint[];
  stops: { id: string; name: string; lat: number; lon: number; passed: boolean }[];
  approximate: boolean;
};

type RouteSectionProps = {
  vehicleKey: string;
  /** Called with the route to draw while it is shown, null when it is not. */
  onRouteChange: (route: RouteOverlay | null) => void;
};

const NEXT_STOPS = 3;
const OFF_ROUTE_METERS = 150;

export function RouteSection({ vehicleKey, onRouteChange }: RouteSectionProps) {
  const t = useTranslations("route");
  const vehicle = useLiveVehicle(vehicleKey);
  const tripId = vehicle?.tripId ?? null;
  const lat = vehicle?.lat;
  const lon = vehicle?.lon;
  // The stop the feed says the vehicle is heading to (or standing at).
  const currentStopId = vehicle?.stopId ?? null;

  const [show, setShow] = useState(false);
  // Reset the toggle during render when the vehicle changes (React's "adjust state on prop change" pattern).
  const [shownFor, setShownFor] = useState(vehicleKey);
  if (shownFor !== vehicleKey) {
    setShownFor(vehicleKey);
    setShow(false);
  }

  const query = useTripRoute(tripId, show);
  const route = query.data;

  // Where each stop lies along the path. Depends on the route only, not on every vehicle update.
  const stopProjections = useMemo(
    () => (route ? locateStopsAlongPath(route.stops, route.path) : []),
    [route],
  );

  const view = useMemo(() => {
    if (!show || !route || lat === undefined || lon === undefined) return null;
    const position = { lat, lon };
    const hasPath = route.path.length > 1;

    // Which stops are behind the vehicle comes from the feed, not from geometry: every position
    // names the stop the vehicle is heading to or standing at. Geometry alone gets this wrong on
    // routes that use the same road twice. Only without that stop do we fall back to geometry.
    const current = currentStopId ? route.stops.findIndex((stop) => stop.id === currentStopId) : -1;

    // Look for the vehicle only between the previous stop and the current one on the path.
    let window: SegmentWindow | undefined;
    if (hasPath && current >= 0) {
      window = {
        from: current > 0 ? stopProjections[current - 1].segmentIndex : 0,
        to: stopProjections[current].segmentIndex,
      };
    }

    const { passed, remaining, offRouteMeters } = splitPathAt(route.path, position, window);
    const vehicleAlong = hasPath ? projectOntoPath(position, route.path, window).alongMeters : -1;
    const stops = route.stops.map((stop, index) => ({
      ...stop,
      passed: current >= 0 ? index < current : hasPath && stopProjections[index].alongMeters < vehicleAlong,
    }));
    const overlay: RouteOverlay = { passed, remaining, stops, approximate: route.pathIsApproximate };
    return { overlay, offRouteMeters, nextStops: stops.filter((stop) => !stop.passed).slice(0, NEXT_STOPS) };
  }, [show, route, lat, lon, currentStopId, stopProjections]);

  const overlay = view?.overlay ?? null;
  useEffect(() => {
    onRouteChange(overlay);
  }, [overlay, onRouteChange]);

  // Clear the map when the vehicle changes or the panel goes away.
  useEffect(() => () => onRouteChange(null), [vehicleKey, onRouteChange]);

  let body: React.ReactNode = null;
  if (show) {
    if (!tripId) {
      body = <EmptyState title={t("noTrip")} hint={t("noTripHint")} />;
    } else if (query.isPending) {
      body = <LoadingState />;
    } else if (query.isError) {
      body =
        query.error instanceof ApiError && query.error.status === 404 ? (
          <EmptyState title={t("notFound")} />
        ) : (
          <ErrorState message={query.error.message} onRetry={() => void query.refetch()} />
        );
    } else if (route && view) {
      body = (
        <div className="mt-3 space-y-2 text-sm">
          <p>
            <span className="text-muted-foreground">{t("line")}: </span>
            {lineLabel(route.line ?? undefined, null) ?? "–"}
          </p>
          <div>
            <p className="text-muted-foreground">{t("nextStops")}</p>
            {view.nextStops.length > 0 ? (
              <ol className="list-inside list-decimal">
                {view.nextStops.map((stop) => (
                  <li key={stop.id}>{stop.name}</li>
                ))}
              </ol>
            ) : (
              <p>{t("noNextStops")}</p>
            )}
          </div>
          {route.pathIsApproximate ? <p className="text-xs text-muted-foreground">{t("approximate")}</p> : null}
          {view.offRouteMeters > OFF_ROUTE_METERS ? (
            <p role="status" className="text-xs text-muted-foreground">
              {t("offRoute")}
            </p>
          ) : null}
          <p className="text-xs text-muted-foreground">{t("legend")}</p>
        </div>
      );
    }
  }

  return (
    <section aria-labelledby="route-title" className="border-t p-4">
      <div className="flex items-center justify-between gap-2">
        <h3 id="route-title" className="text-sm font-semibold">
          {t("title")}
        </h3>
        <Button
          variant={show ? "default" : "outline"}
          size="sm"
          aria-pressed={show}
          onClick={() => setShow((on) => !on)}
        >
          {show ? t("hide") : t("show")}
        </Button>
      </div>
      {body}
    </section>
  );
}
