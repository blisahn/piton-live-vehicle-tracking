"use client";

import { useMemo, type ReactNode } from "react";
import { useFormatter, useNow, useTranslations } from "next-intl";
import { XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { lineLabel, stopLabel, useLines, useStops } from "@/entities/transit";
import { useConnectionStore, useLiveVehicle, useLiveVehicleCount } from "@/entities/vehicle";
import { EmptyState, LoadingState } from "@/shared/ui/state-views";

type VehiclePanelProps = { vehicleKey: string; onClose: () => void; children?: ReactNode };

type StatusKey = "0" | "1" | "2";
type OccupancyKey = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8";
const STATUS_KEYS: Record<number, StatusKey> = { 0: "0", 1: "1", 2: "2" };
const OCCUPANCY_KEYS: Record<number, OccupancyKey> = {
  0: "0", 1: "1", 2: "2", 3: "3", 4: "4", 5: "5", 6: "6", 7: "7", 8: "8",
};

export function VehiclePanel({ vehicleKey, onClose, children }: VehiclePanelProps) {
  const t = useTranslations("vehicle");
  const format = useFormatter();
  const now = useNow({ updateInterval: 1000 });
  const vehicle = useLiveVehicle(vehicleKey);
  const count = useLiveVehicleCount();
  const status = useConnectionStore((state) => state.status);
  const lines = useLines();
  const stopId = vehicle?.stopId ?? null;
  // A new array each render is fine: useStops sorts the ids and keys its query by their content.
  const stops = useStops(useMemo(() => (stopId ? [stopId] : []), [stopId]));

  const dash = t("none");
  const text = (value: string | null) => value ?? dash;

  let body: ReactNode;
  if (!vehicle && count === 0 && status === "connecting") {
    body = <LoadingState />;
  } else if (!vehicle) {
    body = <EmptyState title={t("empty")} hint={t("emptyHint")} />;
  } else {
    const statusKey = vehicle.status === null ? undefined : STATUS_KEYS[vehicle.status];
    const occupancyKey = vehicle.occupancy === null ? undefined : OCCUPANCY_KEYS[vehicle.occupancy];
    const recordedAt = new Date(vehicle.recordedAt);
    // A vehicle's clock can run a second or two ahead of the viewer's; "in 1 second" would be nonsense.
    const elapsedSince = recordedAt > now ? now : recordedAt;
    const rows: [string, ReactNode][] = [
      [t("fields.route"), text(lineLabel(vehicle.routeId ? lines.get(vehicle.routeId) : undefined, vehicle.routeId))],
      [t("fields.trip"), text(vehicle.tripId)],
      [t("fields.stop"), text(stopLabel(vehicle.stopId ? stops.get(vehicle.stopId) : undefined, vehicle.stopId))],
      [t("fields.status"), statusKey ? t(`status.${statusKey}`) : dash],
      [t("fields.occupancy"), occupancyKey ? t(`occupancy.${occupancyKey}`) : dash],
      [
        t("fields.speed"),
        vehicle.speed === null ? dash : t("units.speed", { value: Math.round(vehicle.speed * 3.6) }),
      ],
      [
        t("fields.bearing"),
        vehicle.bearing === null ? dash : t("units.bearing", { value: Math.round(vehicle.bearing) }),
      ],
      [
        t("fields.lastUpdate"),
        `${format.dateTime(recordedAt, { timeStyle: "medium" })} (${format.relativeTime(elapsedSince, now)})`,
      ],
    ];
    body = (
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 p-4 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="min-w-0 break-words">{value}</dd>
          </div>
        ))}
      </dl>
    );
  }

  return (
    <aside className="flex h-full w-full flex-col bg-background">
      <header className="flex items-center justify-between gap-2 border-b p-4">
        <h2 className="min-w-0 truncate font-semibold">{t("title", { key: vehicleKey })}</h2>
        <Button variant="ghost" size="icon" aria-label={t("close")} onClick={onClose}>
          <XIcon aria-hidden />
        </Button>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {status === "reconnecting" || status === "disconnected" ? (
          <p role="status" className="bg-muted px-4 py-2 text-xs text-muted-foreground">
            {t("stale")}
          </p>
        ) : null}
        {body}
        {children}
      </div>
    </aside>
  );
}
