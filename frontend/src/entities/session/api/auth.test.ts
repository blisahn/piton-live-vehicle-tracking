import { afterEach, describe, expect, it, vi } from "vitest";
import { useSessionStore } from "../model/session-store";
import { refreshSession } from "./auth";

const signedIn = () =>
  useSessionStore.setState({
    status: "authenticated",
    accessToken: "old",
    expiresAt: Date.now() + 60_000,
    user: { id: "u1", email: "a@b.c", role: "user" },
  });

describe("refreshSession", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("ends the session when the server rejects the refresh token", async () => {
    signedIn();
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ detail: "no" }), { status: 401 })));

    expect(await refreshSession()).toBe(false);
    expect(useSessionStore.getState().status).toBe("anonymous");
  });

  it("keeps the session when the server cannot be reached", async () => {
    signedIn();
    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new TypeError("Failed to fetch"))));

    expect(await refreshSession()).toBe(false);
    expect(useSessionStore.getState().status).toBe("authenticated");
  });

  it("keeps the session when the server is down or busy", async () => {
    signedIn();
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 503 })));

    expect(await refreshSession()).toBe(false);
    expect(useSessionStore.getState().status).toBe("authenticated");
  });
});
