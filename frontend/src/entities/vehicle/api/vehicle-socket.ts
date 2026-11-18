import { useAlertsVersionStore } from "../model/alerts-version-store";
import type { ConnectionStatus } from "../model/connection-store";
import type { LiveVehicleStore } from "../model/live-vehicle-store";
import { serverMessageSchema } from "../model/schemas";
import {
  effectiveSubscription,
  subscriptionKey,
  useSubscriptionStore,
  type Subscription,
} from "../model/subscription-store";

const INITIAL_RETRY_MS = 1_000;
const MAX_RETRY_MS = 15_000;
/** After this many failed attempts in a row the UI says "disconnected" instead of "reconnecting". */
const ATTEMPTS_BEFORE_DISCONNECTED = 4;
/** The server sends a heartbeat every 10 s when idle; this much silence means the connection is dead. */
const SILENCE_TIMEOUT_MS = 25_000;
/** Pans and filter keystrokes arrive in bursts; one subscription message follows the burst. */
const SUBSCRIBE_DEBOUNCE_MS = 300;
/** A long line list would make the connect URL too long for servers (about 8 KB); it is sent as a message instead. */
const MAX_LINES_IN_URL = 100;

/** How the socket gets its access token and renews it; injected so the socket knows nothing about sessions. */
export type SocketAuth = {
  getToken: () => string | null;
  /** True when the token is missing or about to expire. */
  isExpiring: () => boolean;
  /** Renews the token; false when there is no valid session any more. */
  refresh: () => Promise<boolean>;
};

/** The server closes with this code when the access token has expired. */
const CLOSE_TOKEN_EXPIRED = 4401;

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
  private subscribeTimer: ReturnType<typeof setTimeout> | null = null;
  private unsubscribeStore: (() => void) | null = null;
  /** What the server currently has for this connection (what the URL carried, or the last message sent). */
  private lastSentKey = "";
  private stopped = true;
  /** A token renewal is in progress before the next connect. */
  private connecting = false;

  constructor(
    private readonly url: string,
    private readonly store: LiveVehicleStore,
    private readonly onStatus: (status: ConnectionStatus) => void,
    private readonly auth: SocketAuth,
  ) {}

  start(): void {
    if (!this.stopped) return;
    this.stopped = false;
    window.addEventListener("online", this.retryNow);
    document.addEventListener("visibilitychange", this.retryNow);
    this.unsubscribeStore = useSubscriptionStore.subscribe(() => this.scheduleSubscribe());
    this.onStatus("connecting");
    void this.connect();
  }

  stop(): void {
    this.stopped = true;
    window.removeEventListener("online", this.retryNow);
    document.removeEventListener("visibilitychange", this.retryNow);
    this.unsubscribeStore?.();
    this.unsubscribeStore = null;
    this.clearTimers();
    this.socket?.close();
    this.socket = null;
  }

  /** Makes sure the token is fresh, then opens; without a session there is nothing to connect to. */
  private async connect(): Promise<void> {
    if (this.connecting || this.stopped) return;
    if (this.auth.isExpiring()) {
      this.connecting = true;
      const renewed = await this.auth.refresh().finally(() => (this.connecting = false));
      // No session: the session store goes anonymous and the app unmounts this socket.
      if (!renewed || this.stopped) return;
    }
    this.open();
  }

  private open(): void {
    // Every (re)connect starts from the current subscription, so the first snapshot is already filtered.
    const subscription = effectiveSubscription(useSubscriptionStore.getState());
    const { url, carried } = this.connectUrl(subscription);
    this.lastSentKey = subscriptionKey(carried);
    const socket = new WebSocket(url);
    this.socket = socket;

    // Anything that changed while the connection was being set up (or was not carried by the URL) goes out now.
    socket.onopen = () => {
      if (socket === this.socket) this.scheduleSubscribe();
    };

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

    socket.onclose = (event) => {
      if (socket !== this.socket || this.stopped) return;
      this.socket = null;
      if (event.code === CLOSE_TOKEN_EXPIRED) {
        // Expected every few minutes: renew the token and reconnect right away, without counting a failure.
        this.clearTimers();
        void this.auth.refresh().then((renewed) => {
          if (renewed && !this.stopped) this.open();
        });
        return;
      }
      this.scheduleRetry();
    };
    // An error is always followed by a close event; the close handler does the work.
    socket.onerror = () => {};

    this.armSilenceTimer();
  }

  /** The connect URL with the subscription as query parameters, and the part of it the URL really carries. */
  private connectUrl(subscription: Subscription): { url: string; carried: Subscription } {
    const carried: Subscription = {
      bbox: subscription.bbox,
      lines: subscription.lines.length <= MAX_LINES_IN_URL ? subscription.lines : [],
      keys: subscription.keys,
    };
    const params = new URLSearchParams();
    // A browser cannot set headers on a WebSocket, so the token travels in the query string.
    const token = this.auth.getToken();
    if (token) params.set("access_token", token);
    if (carried.bbox) params.set("bbox", carried.bbox.join(","));
    if (carried.lines.length > 0) params.set("lines", carried.lines.join(","));
    if (carried.keys.length > 0) params.set("keys", carried.keys.join(","));
    const query = params.toString();
    return { url: query ? `${this.url}?${query}` : this.url, carried };
  }

  /** Coalesces changes: the message goes out once the store has been quiet for a moment. */
  private scheduleSubscribe(): void {
    if (this.stopped) return;
    if (this.subscribeTimer) clearTimeout(this.subscribeTimer);
    this.subscribeTimer = setTimeout(() => {
      this.subscribeTimer = null;
      this.sendSubscription();
    }, SUBSCRIBE_DEBOUNCE_MS);
  }

  private sendSubscription(): void {
    const socket = this.socket;
    // Not open: the next connect (or the open event) picks the current subscription up.
    if (!socket || socket.readyState !== WebSocket.OPEN) return;
    const subscription = effectiveSubscription(useSubscriptionStore.getState());
    const key = subscriptionKey(subscription);
    if (key === this.lastSentKey) return;
    this.lastSentKey = key;
    socket.send(JSON.stringify({ type: "subscribe", ...subscription }));
  }

  private scheduleRetry(): void {
    this.clearTimers();
    this.failedAttempts++;
    this.onStatus(this.failedAttempts >= ATTEMPTS_BEFORE_DISCONNECTED ? "disconnected" : "reconnecting");

    // 1 s, 2 s, 4 s … capped, with jitter so many clients do not reconnect in the same instant.
    const backoff = Math.min(INITIAL_RETRY_MS * 2 ** (this.failedAttempts - 1), MAX_RETRY_MS);
    const delay = backoff * (0.5 + Math.random() * 0.5);
    this.retryTimer = setTimeout(() => void this.connect(), delay);
  }

  /** The network came back or the tab became visible: do not wait out the backoff. */
  private readonly retryNow = (): void => {
    if (this.stopped || this.socket !== null || document.visibilityState === "hidden") return;
    this.clearTimers();
    void this.connect();
  };

  private armSilenceTimer(): void {
    if (this.silenceTimer) clearTimeout(this.silenceTimer);
    // Closing triggers onclose, which schedules the retry.
    this.silenceTimer = setTimeout(() => this.socket?.close(), SILENCE_TIMEOUT_MS);
  }

  private clearTimers(): void {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    if (this.silenceTimer) clearTimeout(this.silenceTimer);
    if (this.subscribeTimer) clearTimeout(this.subscribeTimer);
    this.retryTimer = this.silenceTimer = this.subscribeTimer = null;
  }
}
