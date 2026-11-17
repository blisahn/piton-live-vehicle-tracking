import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { getJson } from "@/shared/api/http";

export const systemStatusSchema = z.object({
  serverTime: z.string(),
  // null until the first read succeeds
  lastSuccessfulReadAt: z.string().nullable(),
  lastReadDurationMs: z.number(),
  lastEntityCount: z.number(),
  lastChangedCount: z.number(),
  lastRejectedCount: z.number(),
  successfulReads: z.number(),
  failedReads: z.number(),
  // null while the database is unreachable
  rowsWrittenLastHour: z.number().nullable(),
  databaseAvailable: z.boolean(),
  pendingSpoolBatches: z.number(),
  connectedClients: z.number(),
  trackedVehicles: z.number(),
  // Added later; optional so the screen still works against an older backend.
  tripUpdatesLastSuccessfulReadAt: z.string().nullable().optional(),
  trackedTrips: z.number().optional(),
});

export type SystemStatus = z.infer<typeof systemStatusSchema>;

export function fetchSystemStatus(signal?: AbortSignal) {
  return getJson("/api/status", systemStatusSchema, signal);
}

export function useSystemStatus() {
  return useQuery({
    queryKey: ["system-status"],
    queryFn: ({ signal }) => fetchSystemStatus(signal),
    refetchInterval: 5000,
    // No point polling while the tab is hidden.
    refetchIntervalInBackground: false,
  });
}
