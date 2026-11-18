import type { z } from "zod";
import { env } from "@/shared/config/env";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * What the HTTP layer needs from the session, handed in from outside so that shared code
 * does not depend on the session entity (dependency inversion).
 */
export type HttpAuth = {
  getToken: () => string | null;
  /** Gets a new access token; resolves to false when there is no valid session any more. */
  refresh: () => Promise<boolean>;
  /** Called when the session could not be renewed. */
  onUnauthorized: () => void;
};

let auth: HttpAuth | null = null;
let pendingRefresh: Promise<boolean> | null = null;

export function configureHttpAuth(hooks: HttpAuth | null): void {
  auth = hooks;
  pendingRefresh = null;
}

/** Parallel 401s share one refresh: the refresh token rotates, so a second concurrent call would fail. */
function refreshOnce(hooks: HttpAuth): Promise<boolean> {
  pendingRefresh ??= hooks
    .refresh()
    .catch(() => false)
    .finally(() => {
      pendingRefresh = null;
    });
  return pendingRefresh;
}

async function toApiError(response: Response): Promise<ApiError> {
  // The backend answers errors as RFC 9457 problem details; "detail" is the readable part.
  const problem: unknown = await response.json().catch(() => null);
  const detail =
    typeof problem === "object" && problem !== null && "detail" in problem
      ? String(problem.detail)
      : response.statusText;
  return new ApiError(response.status, detail);
}

function authorizedGet(path: string, signal: AbortSignal | undefined): Promise<Response> {
  const token = auth?.getToken();
  return fetch(`${env.apiUrl}${path}`, {
    signal,
    headers: { Accept: "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  });
}

/**
 * GETs a JSON resource from the backend and checks it against a schema.
 * A response that does not match fails here, at the edge, instead of deep inside a component.
 * An expired access token (401) is renewed once and the request is repeated.
 */
export async function getJson<TSchema extends z.ZodType>(
  path: string,
  schema: TSchema,
  signal?: AbortSignal,
): Promise<z.infer<TSchema>> {
  let response = await authorizedGet(path, signal);

  if (response.status === 401 && auth) {
    if (await refreshOnce(auth)) {
      response = await authorizedGet(path, signal);
    } else {
      auth.onUnauthorized();
      throw await toApiError(response);
    }
  }

  if (!response.ok) throw await toApiError(response);
  return schema.parse(await response.json());
}

/**
 * POSTs JSON for the auth calls. The refresh cookie travels with it (credentials), and a 401 here
 * means "wrong credentials", so it never goes through the refresh logic above.
 */
export async function postJson<TSchema extends z.ZodType>(
  path: string,
  body: unknown,
  schema: TSchema,
  signal?: AbortSignal,
): Promise<z.infer<TSchema>> {
  const response = await sendAuthRequest(path, body, signal);
  return schema.parse(await response.json());
}

/** POST without a response body (for example logout, which answers 204). */
export async function postEmpty(path: string, signal?: AbortSignal): Promise<void> {
  await sendAuthRequest(path, undefined, signal);
}

async function sendAuthRequest(path: string, body: unknown, signal?: AbortSignal): Promise<Response> {
  const token = auth?.getToken();
  const response = await fetch(`${env.apiUrl}${path}`, {
    method: "POST",
    signal,
    credentials: "include",
    headers: {
      Accept: "application/json",
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) throw await toApiError(response);
  return response;
}
