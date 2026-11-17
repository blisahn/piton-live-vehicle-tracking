import { useInfiniteQuery } from "@tanstack/react-query";
import { z } from "zod";
import { vehicleSchema } from "@/entities/vehicle";
import { getJson } from "@/shared/api/http";
import type { TimeRange } from "../model/time-range";

const PAGE_SIZE = 100;

const historyPageSchema = z.object({
  items: z.array(vehicleSchema),
  nextCursor: z.string().nullable(),
});

export type HistoryPage = z.infer<typeof historyPageSchema>;

export function fetchHistoryPage(
  vehicleKey: string,
  range: TimeRange,
  cursor: string | null,
  signal?: AbortSignal,
): Promise<HistoryPage> {
  const params = [
    `from=${encodeURIComponent(range.from)}`,
    `to=${encodeURIComponent(range.to)}`,
    `limit=${PAGE_SIZE}`,
  ];
  if (cursor) params.push(`cursor=${encodeURIComponent(cursor)}`);
  const path = `/api/vehicles/${encodeURIComponent(vehicleKey)}/history?${params.join("&")}`;
  return getJson(path, historyPageSchema, signal);
}

export function useVehicleHistory(vehicleKey: string, range: TimeRange) {
  return useInfiniteQuery({
    queryKey: ["vehicle-history", vehicleKey, range.from, range.to],
    queryFn: ({ pageParam, signal }) => fetchHistoryPage(vehicleKey, range, pageParam, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
  });
}
