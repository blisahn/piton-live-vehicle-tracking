import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { ApiError, getJson } from "@/shared/api/http";

const progressStopSchema = z.object({
  sequence: z.number(),
  stopId: z.string(),
  name: z.string().nullable(),
  aimedArrival: z.string().nullable(),
  expectedArrival: z.string().nullable(),
  aimedDeparture: z.string().nullable(),
  expectedDeparture: z.string().nullable(),
  arrivalDelaySeconds: z.number().nullable(),
  departureDelaySeconds: z.number().nullable(),
  skipped: z.boolean(),
});

const progressSchema = z.object({
  tripId: z.string(),
  startDate: z.string().nullable(),
  updatedAt: z.string().nullable(),
  canceled: z.boolean(),
  // Delay at the vehicle's next upcoming stop.
  delaySeconds: z.number().nullable(),
  stops: z.array(progressStopSchema),
});

export type TripProgress = z.infer<typeof progressSchema>;
export type ProgressStop = z.infer<typeof progressStopSchema>;

/** The backend refreshes trip updates every 30 s. */
const REFRESH_MS = 30_000;

/** Resolves to null when the trip has no realtime schedule (404): that is a normal state, not an error. */
async function fetchTripProgress(tripId: string, signal?: AbortSignal): Promise<TripProgress | null> {
  try {
    return await getJson(`/api/trips/${encodeURIComponent(tripId)}/progress`, progressSchema, signal);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export function useTripProgress(tripId: string | null) {
  return useQuery({
    queryKey: ["trip-progress", tripId],
    queryFn: ({ signal }) => fetchTripProgress(tripId ?? "", signal),
    enabled: tripId !== null,
    refetchInterval: REFRESH_MS,
    refetchIntervalInBackground: false,
    retry: 1,
  });
}
