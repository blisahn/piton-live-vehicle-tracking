import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { z } from "zod";
import { ApiError, configureHttpAuth, getJson } from "./http";

const schema = z.object({ ok: z.boolean() });
const json = (status: number, body: unknown = { ok: true }) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

describe("getJson authentication", () => {
  let token: string | null;
  const fetchMock = vi.fn<typeof fetch>();
  const refresh = vi.fn<() => Promise<boolean>>();
  const onUnauthorized = vi.fn();

  beforeEach(() => {
    token = "old";
    fetchMock.mockReset();
    refresh.mockReset();
    onUnauthorized.mockReset();
    vi.stubGlobal("fetch", fetchMock);
    configureHttpAuth({ getToken: () => token, refresh, onUnauthorized });
  });

  afterEach(() => {
    configureHttpAuth(null);
    vi.unstubAllGlobals();
  });

  const bearer = (call: number) => (fetchMock.mock.calls[call][1]?.headers as Record<string, string>).Authorization;

  it("sends the access token as a bearer header", async () => {
    fetchMock.mockResolvedValueOnce(json(200));
    await getJson("/api/x", schema);
    expect(bearer(0)).toBe("Bearer old");
  });

  it("refreshes once on 401 and repeats the request with the new token", async () => {
    refresh.mockImplementation(async () => {
      token = "new";
      return true;
    });
    fetchMock.mockResolvedValueOnce(json(401, { detail: "expired" })).mockResolvedValueOnce(json(200));

    await expect(getJson("/api/x", schema)).resolves.toEqual({ ok: true });
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(bearer(1)).toBe("Bearer new");
  });

  it("shares one refresh between parallel 401s", async () => {
    refresh.mockImplementation(async () => {
      await Promise.resolve();
      token = "new";
      return true;
    });
    fetchMock.mockImplementation(async () => (token === "new" ? json(200) : json(401)));

    await Promise.all([getJson("/api/a", schema), getJson("/api/b", schema), getJson("/api/c", schema)]);
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("reports the session as lost and throws 401 when the refresh fails", async () => {
    refresh.mockResolvedValue(false);
    fetchMock.mockResolvedValue(json(401, { detail: "expired" }));

    const error = await getJson("/api/x", schema).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("does not refresh for other errors such as 403", async () => {
    fetchMock.mockResolvedValueOnce(json(403, { detail: "no" }));
    await expect(getJson("/api/x", schema)).rejects.toMatchObject({ status: 403 });
    expect(refresh).not.toHaveBeenCalled();
  });
});
