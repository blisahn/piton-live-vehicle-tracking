import type { SystemStatus } from "../api/status";

export type HealthLevel = "healthy" | "degraded" | "down";

const MAX_READ_AGE_MS = 30_000;
const MAX_PENDING_BATCHES = 5;

type HealthInput = Pick<SystemStatus, "serverTime" | "lastSuccessfulReadAt" | "databaseAvailable" | "pendingSpoolBatches">;

/**
 * The read age is measured against the server's own clock (serverTime), because the
 * browser clock may differ. nowMs is only a fallback if serverTime cannot be parsed.
 */
export function deriveHealth(status: HealthInput, nowMs: number): HealthLevel {
  if (status.lastSuccessfulReadAt === null) return "down";

  const serverNowMs = Date.parse(status.serverTime);
  const referenceMs = Number.isNaN(serverNowMs) ? nowMs : serverNowMs;
  const lastReadMs = Date.parse(status.lastSuccessfulReadAt);
  if (Number.isNaN(lastReadMs) || referenceMs - lastReadMs > MAX_READ_AGE_MS) return "down";

  if (!status.databaseAvailable || status.pendingSpoolBatches > MAX_PENDING_BATCHES) return "degraded";
  return "healthy";
}
