import { z } from "zod";
import { ApiError, getJson, postEmpty, postJson } from "@/shared/api/http";
import { useSessionStore } from "../model/session-store";

const userSchema = z.object({
  id: z.string(),
  email: z.string(),
  role: z.enum(["user", "admin"]),
});

const authResultSchema = z.object({
  accessToken: z.string(),
  expiresAt: z.string(),
  user: userSchema,
});

export type Credentials = { email: string; password: string };

async function authenticate(path: string, credentials?: Credentials) {
  const result = credentials
    ? await postJson(path, credentials, authResultSchema)
    : await postJson(path, undefined, authResultSchema);
  useSessionStore.getState().setSession(result);
  return result.user;
}

export const login = (credentials: Credentials) => authenticate("/api/auth/login", credentials);
export const register = (credentials: Credentials) => authenticate("/api/auth/register", credentials);

export const fetchMe = (signal?: AbortSignal) => getJson("/api/auth/me", userSchema, signal);

let pendingRefresh: Promise<boolean> | null = null;

/**
 * Restores or renews the session from the refresh cookie. Resolves to false when it could not.
 * Only the server saying the session is invalid (401/400/403) ends it; a network error or a
 * server that is down or busy (5xx, 429) leaves the session as it is, because the refresh cookie
 * may still be perfectly valid once the server is back.
 * Single flight: the refresh token rotates, so callers that arrive while one refresh is running
 * share its result instead of starting a second one.
 */
export function refreshSession(): Promise<boolean> {
  pendingRefresh ??= authenticate("/api/auth/refresh")
    .then(() => true)
    .catch((error: unknown) => {
      if (error instanceof ApiError && [400, 401, 403].includes(error.status)) useSessionStore.getState().clear();
      return false;
    })
    .finally(() => {
      pendingRefresh = null;
    });
  return pendingRefresh;
}

/** Ends the session on the server and locally; the local session is cleared even if the call fails. */
export async function logout(): Promise<void> {
  try {
    await postEmpty("/api/auth/logout");
  } finally {
    useSessionStore.getState().clear();
  }
}
