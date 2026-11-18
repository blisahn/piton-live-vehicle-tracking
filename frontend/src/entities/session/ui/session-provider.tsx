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
      onUnauthorized: () => useSessionStore.getState().clear(),
    });
    void refreshSession();
    return () => configureHttpAuth(null);
  }, []);

  return children;
}
