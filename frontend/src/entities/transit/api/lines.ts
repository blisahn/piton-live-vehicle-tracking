import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { z } from "zod";
import { getJson } from "@/shared/api/http";

export const lineSchema = z.object({
  /** e.g. "AKT:Line:1001"; the same value as a vehicle's routeId. */
  id: z.string(),
  /** The number riders know, e.g. "M1". */
  publicCode: z.string().nullable(),
  name: z.string().nullable(),
  transportMode: z.string().nullable(),
});

export type Line = z.infer<typeof lineSchema>;

const linesResponseSchema = z.object({ lines: z.array(lineSchema) });

const EMPTY: Map<string, Line> = new Map();

/** Every line the backend knows. New lines keep appearing in the first minutes, hence the refetch. */
export function useLines(): Map<string, Line> {
  const query = useQuery({
    queryKey: ["transit-lines"],
    queryFn: ({ signal }) => getJson("/api/lines", linesResponseSchema, signal),
    refetchInterval: 60_000,
    staleTime: 30_000,
  });
  return useMemo(
    () => (query.data ? new Map(query.data.lines.map((line) => [line.id, line])) : EMPTY),
    [query.data],
  );
}
