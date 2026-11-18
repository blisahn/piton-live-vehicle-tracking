"use client";

import { useEffect, type ReactNode } from "react";
import { configureHttpAuth } from "@/shared/api/http";
import { refreshSession } from "../api/auth";
import { getAccessToken, useSessionStore } from "../model/session-store";

/** Connects the HTTP layer to the session and restores the session from the refresh cookie on load. */
export function SessionProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    configureHttpAuth({
      getToken: getAccessToken,
      refresh: refreshSession,
      // refreshSession already ends the session when the server rejects it; an unreachable server must not.
      onUnauthorized: () => {},
    });

    // Restore the session on load. If the server cannot be reached yet (still starting, restarting),
    // keep trying instead of sending the user to the login page with a cookie that is still valid.
    let timer: ReturnType<typeof setTimeout> | null = null;
    let delay = 2_000;
    const restore = () => {
      void refreshSession().then((ok) => {
        if (ok || useSessionStore.getState().status !== "unknown") return;
        timer = setTimeout(restore, delay);
        delay = Math.min(delay * 2, 15_000);
      });
    };
    restore();
    return () => {
      if (timer) clearTimeout(timer);
      configureHttpAuth(null);
    };
  }, []);

  return children;
}
