import { create } from "zustand";

export type Role = "user" | "admin";
export type SessionUser = { id: string; email: string; role: Role };
export type SessionStatus = "unknown" | "authenticated" | "anonymous";

type SessionState = {
  /** "unknown" until the silent refresh at page load has answered. */
  status: SessionStatus;
  /** Lives in memory only: never in localStorage or sessionStorage, so a script cannot read it from storage. */
  accessToken: string | null;
  expiresAt: number | null;
  user: SessionUser | null;
  setSession: (session: { accessToken: string; expiresAt: string; user: SessionUser }) => void;
  clear: () => void;
};

export const useSessionStore = create<SessionState>((set) => ({
  status: "unknown",
  accessToken: null,
  expiresAt: null,
  user: null,
  setSession: ({ accessToken, expiresAt, user }) =>
    set({ status: "authenticated", accessToken, expiresAt: Date.parse(expiresAt), user }),
  clear: () => set({ status: "anonymous", accessToken: null, expiresAt: null, user: null }),
}));

export const getAccessToken = () => useSessionStore.getState().accessToken;

/** True when there is no token or it expires within the margin. */
export function isTokenExpiring(marginMs = 10_000): boolean {
  const { accessToken, expiresAt } = useSessionStore.getState();
  return !accessToken || (expiresAt !== null && expiresAt - Date.now() < marginMs);
}
