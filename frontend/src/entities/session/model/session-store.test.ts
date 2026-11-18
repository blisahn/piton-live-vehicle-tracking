import { beforeEach, describe, expect, it } from "vitest";
import { isTokenExpiring, useSessionStore } from "./session-store";

const user = { id: "1", email: "a@b.c", role: "user" as const };

describe("session store", () => {
  beforeEach(() => useSessionStore.setState({ status: "unknown", accessToken: null, expiresAt: null, user: null }));

  it("starts unknown, becomes authenticated and then anonymous", () => {
    expect(useSessionStore.getState().status).toBe("unknown");
    useSessionStore.getState().setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 60_000).toISOString(), user });
    expect(useSessionStore.getState()).toMatchObject({ status: "authenticated", accessToken: "t", user });
    useSessionStore.getState().clear();
    expect(useSessionStore.getState()).toMatchObject({ status: "anonymous", accessToken: null, user: null });
  });

  it("knows when the token is about to expire", () => {
    expect(isTokenExpiring()).toBe(true);
    useSessionStore.getState().setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 5_000).toISOString(), user });
    expect(isTokenExpiring()).toBe(true);
    useSessionStore.getState().setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 60_000).toISOString(), user });
    expect(isTokenExpiring()).toBe(false);
  });
});
