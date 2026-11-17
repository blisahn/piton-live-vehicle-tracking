import { create } from "zustand";

type AlertsVersionState = {
  /** null until the server has told us the version once. */
  version: number | null;
  setVersion: (version: number) => void;
};

export const useAlertsVersionStore = create<AlertsVersionState>((set) => ({
  version: null,
  setVersion: (version) => set({ version }),
}));

/** The version of the server's alert set; a change means "fetch the alerts again". */
export function useAlertsVersion(): number | null {
  return useAlertsVersionStore((s) => s.version);
}
