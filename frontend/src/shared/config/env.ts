import { z } from "zod";

// NEXT_PUBLIC_ values are inlined at build time, so each must be read by its full name.
const schema = z.object({
  apiUrl: z.url(),
});

const parsed = schema.parse({
  apiUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080",
});

const apiUrl = parsed.apiUrl.replace(/\/$/, "");

export const env = {
  apiUrl,
  /** Same host as the API, with the scheme switched from http(s) to ws(s). */
  vehicleSocketUrl: `${apiUrl.replace(/^http/, "ws")}/ws/vehicles`,
};
