import { useSessionStore } from "./session-store";

export function useSession() {
  const status = useSessionStore((s) => s.status);
  const user = useSessionStore((s) => s.user);
  return { status, user };
}

export const useIsAdmin = () => useSessionStore((s) => s.user?.role === "admin");
