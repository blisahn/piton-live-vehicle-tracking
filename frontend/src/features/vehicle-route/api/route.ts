import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { lineSchema, stopSchema } from "@/entities/transit";
import { ApiError, getJson } from "@/shared/api/http";

const routeSchema = z.object({
  tripId: z.string(),
  line: lineSchema.nullable(),
  path: z.array(z.object({ lat: z.number(), lon: z.number() })),
  stops: z.array(stopSchema),
  /** True when the backend only had the stops and joined them with straight lines. */
  pathIsApproximate: z.boolean(),
});

export type TripRoute = z.infer<typeof routeSchema>;

export function useTripRoute(tripId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["trip-route", tripId],
    queryFn: ({ signal }) => getJson(`/api/trips/${encodeURIComponent(tripId ?? "")}/route`, routeSchema, signal),
    enabled: enabled && tripId !== null,
    // A trip's plan does not change while it is being driven.
    staleTime: 60 * 60_000,
    // An unknown trip stays unknown; anything else (503 while the catalog loads) gets one more try.
    retry: (failures, error) => !(error instanceof ApiError && error.status === 404) && failures < 1,
  });
}
