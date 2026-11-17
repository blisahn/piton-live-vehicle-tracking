import { useAlertsVersionStore } from "../model/alerts-version-store";
import type { ConnectionStatus } from "../model/connection-store";
import type { LiveVehicleStore } from "../model/live-vehicle-store";
import { serverMessageSchema } from "../model/schemas";

const INITIAL_RETRY_MS = 1_000;
const MAX_RETRY_MS = 15_000;
/** After this many failed attempts in a row the UI says "disconnected" instead of "reconnecting". */
const ATTEMPTS_BEFORE_DISCONNECTED = 4;
/** The server sends a heartbeat every 10 s when idle; this much silence means the connection is dead. */
const SILENCE_TIMEOUT_MS = 25_000;

/**
 * Keeps one WebSocket to the backend alive and feeds what arrives into the store.
 *
 * Recovery never tries to replay what was missed. Every new connection starts with a full
 * snapshot, so "reconnect" is the single answer to a drop, a gap in sequence numbers,
 * a malformed message or a silent connection.
 */
export class VehicleSocket {
  private socket: WebSocket | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private silenceTimer: ReturnType<typeof setTimeout> | null = null;
  private failedAttempts = 0;
  private stopped = true;

  constructor(
    private readonly url: string,
    private readonly store: LiveVehicleStore,
    private readonly onStatus: (status: ConnectionStatus) => void,
  ) {}

  start(): void {
    if (!this.stopped) return;
    this.stopped = false;
    window.addEventListener("online", this.retryNow);
    document.addEventListener("visibilitychange", this.retryNow);
    this.onStatus("connecting");
    this.open();
  }

  stop(): void {
    this.stopped = true;
    window.removeEventListener("online", this.retryNow);
    document.removeEventListener("visibilitychange", this.retryNow);
    this.clearTimers();
    this.socket?.close();
    this.socket = null;
  }

  private open(): void {
    const socket = new WebSocket(this.url);
    this.socket = socket;

    socket.onmessage = (event) => {
      // A socket we already replaced may still deliver a late message; ignore it.
      if (socket !== this.socket) return;
      this.armSilenceTimer();

      const parsed = serverMessageSchema.safeParse(JSON.parse(String(event.data)));
      // Alerts change independently of vehicle positions: no sequence, no effect on the connection status.
      if (parsed.success && parsed.data.type === "alerts") {
        useAlertsVersionStore.getState().setVersion(parsed.data.version);
        return;
      }
      if (!parsed.success || !this.store.apply(parsed.data)) {
        // Unreadable message or a gap in the sequence: start over from a snapshot.
        socket.close();
        return;
      }

      if (this.failedAttempts > 0 || parsed.data.type === "snapshot") {
        this.failedAttempts = 0;
        this.onStatus("connected");
      }
    };

    socket.onclose = () => {
      if (socket !== this.socket || this.stopped) return;
      this.socket = null;
      this.scheduleRetry();
    };
    // An error is always followed by a close event; the close handler does the work.
    socket.onerror = () => {};

    this.armSilenceTimer();
  }

  private scheduleRetry(): void {
    this.clearTimers();
    this.failedAttempts++;
    this.onStatus(this.failedAttempts >= ATTEMPTS_BEFORE_DISCONNECTED ? "disconnected" : "reconnecting");

    // 1 s, 2 s, 4 s … capped, with jitter so many clients do not reconnect in the same instant.
    const backoff = Math.min(INITIAL_RETRY_MS * 2 ** (this.failedAttempts - 1), MAX_RETRY_MS);
    const delay = backoff * (0.5 + Math.random() * 0.5);
    this.retryTimer = setTimeout(() => this.open(), delay);
  }

  /** The network came back or the tab became visible: do not wait out the backoff. */
  private readonly retryNow = (): void => {
    if (this.stopped || this.socket !== null || document.visibilityState === "hidden") return;
    this.clearTimers();
    this.open();
  };

  private armSilenceTimer(): void {
    if (this.silenceTimer) clearTimeout(this.silenceTimer);
    // Closing triggers onclose, which schedules the retry.
    this.silenceTimer = setTimeout(() => this.socket?.close(), SILENCE_TIMEOUT_MS);
  }

  private clearTimers(): void {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    if (this.silenceTimer) clearTimeout(this.silenceTimer);
    this.retryTimer = this.silenceTimer = null;
  }
}
