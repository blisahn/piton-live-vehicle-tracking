import type { ServerMessage, Vehicle } from "./schemas";

type Listener = () => void;

/**
 * Holds the latest position of every vehicle, outside React.
 *
 * Thousands of positions change every few seconds. If they lived in React state, every
 * update would re-render the tree. Here the data is a plain Map; whoever needs it subscribes
 * to exactly what it needs: the map layer to "anything changed", the detail panel to one vehicle.
 */
export class LiveVehicleStore {
  private readonly vehicles = new Map<string, Vehicle>();
  private readonly listeners = new Set<Listener>();
  private readonly vehicleListeners = new Map<string, Set<Listener>>();
  private sequence = 0;
  private version = 0;

  /**
   * Applies a snapshot or delta. Returns false when a delta does not continue from the
   * sequence we are at: something was missed, and the caller must reconnect for a new snapshot.
   */
  apply(message: ServerMessage): boolean {
    // Alerts are routed elsewhere by the socket; they never touch vehicles or the sequence.
    if (message.type === "heartbeat" || message.type === "alerts") return true;

    if (message.type === "snapshot") {
      const changedKeys = new Set([...this.vehicles.keys(), ...message.vehicles.map((v) => v.key)]);
      this.vehicles.clear();
      for (const vehicle of message.vehicles) this.vehicles.set(vehicle.key, vehicle);
      this.sequence = message.seq;
      this.notify(changedKeys);
      return true;
    }

    if (message.fromSeq !== this.sequence + 1) return false;

    const changedKeys = new Set<string>();
    for (const vehicle of message.upserted) {
      this.vehicles.set(vehicle.key, vehicle);
      changedKeys.add(vehicle.key);
    }
    for (const key of message.removed) {
      this.vehicles.delete(key);
      changedKeys.add(key);
    }
    this.sequence = message.toSeq;
    this.notify(changedKeys);
    return true;
  }

  get(key: string): Vehicle | undefined {
    return this.vehicles.get(key);
  }

  /** The live Map itself, for readers that iterate everything (the map layer). Do not mutate. */
  all(): ReadonlyMap<string, Vehicle> {
    return this.vehicles;
  }

  /** Changes whenever any vehicle changes; a cheap value for useSyncExternalStore to compare. */
  getVersion = (): number => this.version;

  getCount = (): number => this.vehicles.size;

  subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  subscribeToVehicle(key: string, listener: Listener): () => void {
    let listeners = this.vehicleListeners.get(key);
    if (!listeners) {
      listeners = new Set();
      this.vehicleListeners.set(key, listeners);
    }
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
      if (listeners.size === 0) this.vehicleListeners.delete(key);
    };
  }

  private notify(changedKeys: Set<string>): void {
    this.version++;
    for (const listener of this.listeners) listener();
    // Usually zero or one vehicle is being watched, so walk the watchers, not the changes.
    for (const [key, listeners] of this.vehicleListeners) {
      if (changedKeys.has(key)) for (const listener of listeners) listener();
    }
  }
}

/** The one store of the app. A module-level instance is safe here: it is only ever touched in the browser. */
export const liveVehicleStore = new LiveVehicleStore();
