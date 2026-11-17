import { create } from "zustand";

/**
 * connecting    first attempt after the page opened
 * connected     messages are arriving
 * reconnecting  the connection dropped and we are retrying; what is on screen may be a few seconds old
 * disconnected  several retries failed (or the browser is offline); still retrying, but data is stale
 */
export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

type ConnectionState = {
  status: ConnectionStatus;
  setStatus: (status: ConnectionStatus) => void;
};

export const useConnectionStore = create<ConnectionState>((set) => ({
  status: "connecting",
  setStatus: (status) => set({ status }),
}));
