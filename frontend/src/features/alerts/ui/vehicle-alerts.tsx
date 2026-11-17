"use client";

import { useTranslations } from "next-intl";
import { useMemo } from "react";
import { Badge } from "@/components/ui/badge";
import { useLines, useStops } from "@/entities/transit";
import { useLiveVehicle } from "@/entities/vehicle";
import { Link } from "@/i18n/navigation";
import { LoadingState } from "@/shared/ui/state-views";
import { useAlerts, type ServiceAlert } from "../api/alerts";
import { AlertCard } from "./alerts-panel";

/**
 * The alerts a rider of this vehicle cares about: those about its own trip and those about its
 * whole line. Alerts about other departures of the same line are left out.
 *
 * Cost: one small request per line (filtered on the server), cached per line and refetched only
 * when the alerts version changes, not on every position update.
 */
export function VehicleAlerts({ vehicleKey }: { vehicleKey: string }) {
  const t = useTranslations("alerts.vehicle");
  const vehicle = useLiveVehicle(vehicleKey);
  // Only these two fields matter; selecting them keeps position updates from re-running the filter.
  const lineId = vehicle?.routeId ?? null;
  const tripId = vehicle?.tripId ?? null;

  const query = useAlerts({ lineId, stopId: null, includeInactive: false, enabled: lineId !== null });
  const relevant = useMemo(() => {
    const alerts = query.data?.alerts ?? [];
    return alerts
      .filter((alert) => alert.tripIds.length === 0 || (tripId !== null && alert.tripIds.includes(tripId)))
      .sort((a, b) => Number(isForTrip(b, tripId)) - Number(isForTrip(a, tripId)));
  }, [query.data, tripId]);

  const lines = useLines();
  const stopIds = useMemo(() => [...new Set(relevant.flatMap((alert) => alert.stopIds))].slice(0, 200), [relevant]);
  const stops = useStops(stopIds);

  // Nothing to say for a vehicle without a line; the panel already shows that it has no trip.
  if (lineId === null) return null;

  return (
    <section aria-labelledby="vehicle-alerts-title" className="border-t p-4">
      <div className="flex items-center justify-between gap-2">
        <h3 id="vehicle-alerts-title" className="text-sm font-semibold">
          {t("title")}
          {relevant.length > 0 ? (
            <Badge variant="destructive" className="ml-2">
              {relevant.length}
            </Badge>
          ) : null}
        </h3>
        <Link href={{ pathname: "/alerts", query: { line: lineId } }} className="text-xs underline">
          {t("seeAll")}
        </Link>
      </div>
      <div className="mt-3 space-y-2">
        {query.isPending ? (
          <LoadingState />
        ) : relevant.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("none")}</p>
        ) : (
          relevant.map((alert) => (
            <div key={alert.id} className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground">
                {isForTrip(alert, tripId) ? t("thisTrip") : t("lineWide")}
              </p>
              <AlertCard alert={alert} lines={lines} stops={stops} isNew={false} />
            </div>
          ))
        )}
      </div>
    </section>
  );
}

function isForTrip(alert: ServiceAlert, tripId: string | null): boolean {
  return tripId !== null && alert.tripIds.includes(tripId);
}
