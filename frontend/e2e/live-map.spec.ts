import type { Page } from "@playwright/test";
import { adminHeaders, expect, test } from "./fixtures";

const API_URL = process.env.E2E_API_URL ?? "http://localhost:8080";

type ApiVehicle = { key: string; recordedAt: string };

/** A vehicle that reported in the last minute, so it has recent history and is still tracked. */
async function pickActiveVehicle(page: Page): Promise<ApiVehicle> {
  const response = await page.request.get(`${API_URL}/api/vehicles`, { headers: await adminHeaders(page) });
  const { vehicles } = (await response.json()) as { vehicles: ApiVehicle[] };
  const active = vehicles.find((v) => Date.now() - Date.parse(v.recordedAt) < 60_000);
  expect(active, "the backend should be tracking at least one active vehicle").toBeTruthy();
  return active!;
}

const connectionBadge = (page: Page) => page.getByRole("status", { name: /^Connection:/ });
// "N vehicles on screen", or "N matching vehicles, M on screen" while a filter is set.
const vehicleCount = (page: Page) => page.getByText(/[\d,.]+ (matching )?vehicles?(, [\d,.]+)? on screen/);

test("the map goes live and counts the vehicles it shows", async ({ page }) => {
  await page.goto("/en");

  await expect(connectionBadge(page)).toHaveText("Live");
  await expect(vehicleCount(page)).toHaveText(/^[1-9][\d,.]* (matching )?vehicles?/);
  await expect(page.getByRole("application")).toBeVisible();
});

test("a selected vehicle shows live details and its recent positions", async ({ page }) => {
  const vehicle = await pickActiveVehicle(page);
  await page.goto(`/en?vehicle=${encodeURIComponent(vehicle.key)}`);

  await expect(page.getByRole("heading", { name: vehicle.key })).toBeVisible();

  // Server-side paged history of the last 3 hours.
  await page.getByRole("combobox", { name: "Time range" }).click();
  await page.getByRole("option", { name: "Last 3 hours" }).click();
  await expect(page.locator("table tbody tr").first()).toBeVisible();

  await page.getByRole("button", { name: "Show measured path" }).click();
  await expect(page.getByRole("button", { name: "Hide measured path" })).toHaveAttribute("aria-pressed", "true");

  // The planned route comes from a second API; some trips have none, which is a valid answer too.
  await page.getByRole("button", { name: "Show route" }).click();
  await expect(page.getByRole("button", { name: "Hide route" })).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByText(/Next stops|No planned route is available|No trip information|No stops left/).first(),
  ).toBeVisible();

  await page.getByRole("button", { name: /close/i }).click();
  await expect(page).not.toHaveURL(/vehicle=/);
});

test("filter and selection live in the URL and survive reload and language switch", async ({ page }) => {
  const vehicle = await pickActiveVehicle(page);
  const operator = vehicle.key.split(":")[0];
  await page.goto(`/en?vehicle=${encodeURIComponent(vehicle.key)}`);

  await page.getByLabel("Route or vehicle number").fill(operator);
  await expect(page).toHaveURL(new RegExp(`q=${operator}`));

  await page.reload();
  await expect(page.getByLabel("Route or vehicle number")).toHaveValue(operator);
  await expect(page.getByRole("heading", { name: vehicle.key })).toBeVisible();

  await page.getByRole("button", { name: "Türkçe" }).click();
  await expect(page).toHaveURL(/\/tr\?/);
  await expect(page).toHaveURL(new RegExp(`q=${operator}`));
  await expect(page).toHaveURL(/vehicle=/);
});

test("when the connection drops the app says so and recovers by itself", async ({ page }) => {
  let allowConnections = true;
  const openSockets = new Set<{ close: () => Promise<void> }>();

  // Sit between the browser and the backend so the test can cut the connection at will.
  await page.routeWebSocket(/\/ws\/vehicles(\?.*)?$/, (socket) => {
    if (!allowConnections) {
      void socket.close();
      return;
    }
    socket.connectToServer();
    openSockets.add(socket);
  });

  await page.goto("/en");
  await expect(connectionBadge(page)).toHaveText("Live");

  allowConnections = false;
  for (const socket of openSockets) await socket.close();
  await expect(connectionBadge(page)).toHaveText(/Reconnecting|Disconnected/);

  allowConnections = true;
  await expect(connectionBadge(page)).toHaveText("Live", { timeout: 30_000 });
  // Back in sync: a fresh snapshot repopulated the store.
  await expect(vehicleCount(page)).toHaveText(/^[1-9][\d,.]* (matching )?vehicles?/);
});

test("the status screen shows the four required numbers", async ({ page }) => {
  await page.goto("/en/status");

  for (const label of [/last successful read/i, /rows written/i, /failed reads/i, /connected clients/i]) {
    await expect(page.getByText(label).first()).toBeVisible();
  }
});
