import { expect, test as base, type APIRequestContext, type Page } from "@playwright/test";

export const API_URL = process.env.E2E_API_URL ?? "http://localhost:8080";

export const adminCredentials = {
  email: process.env.E2E_ADMIN_EMAIL ?? "admin@vehicle-tracking.local",
  password: process.env.E2E_ADMIN_PASSWORD ?? "admin12345",
};

/** Signs in through the API; the refresh cookie lands in the request's browser context. Returns the access token. */
export async function loginAsAdmin(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${API_URL}/api/auth/login`, { data: adminCredentials });
  expect(response.ok(), "the admin account should exist (E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD)").toBe(true);
  return ((await response.json()) as { accessToken: string }).accessToken;
}

/**
 * Like Playwright's test, but every test starts signed in as the admin. Each test gets its own login
 * (its own session family) because the refresh token rotates: reusing one saved cookie in several
 * tests would look like token theft to the server and revoke the session.
 */
export const test = base.extend({
  context: async ({ context }, provide) => {
    await loginAsAdmin(context.request);
    await provide(context);
  },
});

export { expect };

/** Authorization header for direct API calls from a test (a new login in the page's context). */
export async function adminHeaders(page: Page): Promise<Record<string, string>> {
  return { Authorization: `Bearer ${await loginAsAdmin(page.request)}` };
}
