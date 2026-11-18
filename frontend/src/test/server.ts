import { setupServer } from "msw/node";
import { env } from "@/shared/config/env";

/** Every request a component test makes goes to this fake backend; an unhandled request fails the test. */
export const server = setupServer();

/** Full URL of a backend path, as the HTTP client builds it. */
export const api = (path: string) => `${env.apiUrl}${path}`;
