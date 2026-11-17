import { z } from "zod";

/** One vehicle position, exactly as the backend sends it over REST and WebSocket. */
export const vehicleSchema = z.object({
  /** "codespace:vehicleId", e.g. "RUT:101234". Unique across all operators. */
  key: z.string(),
  /** ISO timestamp of when the vehicle measured this position. */
  recordedAt: z.string(),
  lat: z.number(),
  lon: z.number(),
  bearing: z.number().nullable(),
  speed: z.number().nullable(),
  routeId: z.string().nullable(),
  tripId: z.string().nullable(),
  stopId: z.string().nullable(),
  /** GTFS-Realtime VehicleStopStatus: 0 incoming at, 1 stopped at, 2 in transit to. */
  status: z.number().int().nullable(),
  /** GTFS-Realtime OccupancyStatus: 0 (empty) to 8 (not boardable). */
  occupancy: z.number().int().nullable(),
});

export type Vehicle = z.infer<typeof vehicleSchema>;

const snapshotMessageSchema = z.object({
  type: z.literal("snapshot"),
  seq: z.number().int(),
  vehicles: z.array(vehicleSchema),
});

const deltaMessageSchema = z.object({
  type: z.literal("delta"),
  fromSeq: z.number().int(),
  toSeq: z.number().int(),
  upserted: z.array(vehicleSchema),
  removed: z.array(z.string()),
});

const heartbeatMessageSchema = z.object({
  type: z.literal("heartbeat"),
  seq: z.number().int(),
  serverTime: z.string(),
});

/** Service alerts changed; carries no vehicle sequence number. The alert list itself is fetched over REST. */
const alertsMessageSchema = z.object({
  type: z.literal("alerts"),
  version: z.number().int(),
});

/** Every message the server can send on the vehicle socket. */
export const serverMessageSchema = z.discriminatedUnion("type", [
  snapshotMessageSchema,
  deltaMessageSchema,
  heartbeatMessageSchema,
  alertsMessageSchema,
]);

export type ServerMessage = z.infer<typeof serverMessageSchema>;
