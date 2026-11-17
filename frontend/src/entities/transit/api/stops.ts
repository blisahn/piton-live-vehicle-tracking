import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { z } from "zod";
import { getJson } from "@/shared/api/http";

export const stopSchema = z.object({
  /** e.g. "NSR:Quay:100140"; the same value as a vehicle's stopId. */
  id: z.string(),
  name: z.string(),
  lat: z.number(),
  lon: z.number(),
});

export type Stop = z.infer<typeof stopSchema>;

const stopsResponseSchema = z.object({ stops: z.array(stopSchema) });

const EMPTY: Map<string, Stop> = new Map();

/** Looks stops up by id. Unknown ids are simply missing from the result. */
export function useStops(ids: string[]): Map<string, Stop> {
  // Sorted, so the same set of ids always hits the same cache entry.
  const sorted = useMemo(() => [...ids].sort(), [ids]);
  const query = useQuery({
    queryKey: ["transit-stops", sorted],
    queryFn: ({ signal }) => {
      const params = sorted.map((id) => `ids=${encodeURIComponent(id)}`).join("&");
      return getJson(`/api/stops?${params}`, stopsResponseSchema, signal);
    },
    enabled: sorted.length > 0,
    staleTime: 10 * 60_000,
  });
  return useMemo(
    () => (query.data ? new Map(query.data.stops.map((stop) => [stop.id, stop])) : EMPTY),
    [query.data],
  );
}
