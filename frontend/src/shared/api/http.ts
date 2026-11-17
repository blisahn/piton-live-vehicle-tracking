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
 * GETs a JSON resource from the backend and checks it against a schema.
 * A response that does not match fails here, at the edge, instead of deep inside a component.
 */
export async function getJson<TSchema extends z.ZodType>(
  path: string,
  schema: TSchema,
  signal?: AbortSignal,
): Promise<z.infer<TSchema>> {
  const response = await fetch(`${env.apiUrl}${path}`, {
    signal,
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    // The backend answers errors as RFC 9457 problem details; "detail" is the readable part.
    const problem: unknown = await response.json().catch(() => null);
    const detail =
      typeof problem === "object" && problem !== null && "detail" in problem
        ? String(problem.detail)
        : response.statusText;
    throw new ApiError(response.status, detail);
  }

  return schema.parse(await response.json());
}
