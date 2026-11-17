import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { useAlertsVersion } from "@/entities/vehicle";
import { getJson } from "@/shared/api/http";

const localizedTextSchema = z.object({
  text: z.string(),
  language: z.string().nullable(),
  languageDetected: z.boolean(),
});

const periodSchema = z.object({
  start: z.string().nullable(),
  end: z.string().nullable(),
});

export const alertSchema = z.object({
  id: z.string(),
  active: z.boolean(),
  /** A period ends before it starts: the feed data is wrong, not the app. */
  hasInvalidPeriod: z.boolean(),
  activePeriods: z.array(periodSchema),
  lineIds: z.array(z.string()),
  stopIds: z.array(z.string()),
  tripIds: z.array(z.string()),
  header: z.array(localizedTextSchema),
  description: z.array(localizedTextSchema),
  url: z.string().nullable(),
});

const alertsResponseSchema = z.object({
  version: z.number(),
  generatedAt: z.string().nullable(),
  alerts: z.array(alertSchema),
});

export type ServiceAlert = z.infer<typeof alertSchema>;
export type AlertPeriod = z.infer<typeof periodSchema>;
export type AlertsFilter = { lineId: string | null; stopId: string | null; includeInactive: boolean };

export function useAlerts({ lineId, stopId, includeInactive, enabled = true }: AlertsFilter & { enabled?: boolean }) {
  const filter = { lineId, stopId, includeInactive };
  // A new version from the WebSocket changes the key and so triggers a refetch.
  const version = useAlertsVersion();
  return useQuery({
    queryKey: ["alerts", filter, version],
    enabled,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams();
      if (lineId) params.set("lineId", lineId);
      if (stopId) params.set("stopId", stopId);
      params.set("includeInactive", String(includeInactive));
      return getJson(`/api/alerts?${params.toString()}`, alertsResponseSchema, signal);
    },
    // Keep the list on screen while the next version of the same filter loads, so it does not flash.
    // A different filter (another line, another vehicle) must not show the old line's alerts meanwhile.
    placeholderData: (previous, previousQuery) =>
      previousQuery && JSON.stringify(previousQuery.queryKey[1]) === JSON.stringify(filter) ? previous : undefined,
    // Safety net in case a WebSocket message is missed.
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });
}
