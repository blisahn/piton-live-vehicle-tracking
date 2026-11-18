import type { Page } from "@playwright/test";
import { adminHeaders, expect, test } from "./fixtures";

const API_URL = process.env.E2E_API_URL ?? "http://localhost:8080";

const zoomOut = (page: Page) => page.getByRole("button", { name: "Zoom out" });
const onScreenCount = (page: Page) => page.getByText(/^[\d,.]+ vehicles? on screen$/);
const filteredCount = (page: Page) => page.getByText(/^[1-9][\d,.]* matching vehicles?, [\d,.]+ on screen$/);
const numberIn = async (page: Page) => Number((await onScreenCount(page).innerText()).split(" ")[0].replace(/[,.]/g, ""));

/** A public code of a line that has a vehicle on the road right now. */
async function pickLineCode(page: Page): Promise<string> {
  const vehicles = (await (await page.request.get(`${API_URL}/api/vehicles`, { headers: await adminHeaders(page) })).json()) as {
    vehicles: { routeId: string | null }[];
  };
  const lines = (await (await page.request.get(`${API_URL}/api/lines`, { headers: await adminHeaders(page) })).json()) as {
    lines: { id: string; publicCode: string | null }[];
  };
  const active = new Set(vehicles.vehicles.map((v) => v.routeId));
  const line = lines.lines.find((l) => l.publicCode && active.has(l.id));
  expect(line, "an active vehicle should belong to a known line").toBeTruthy();
  return line!.publicCode!;
}

test("the counter is about what is on screen and grows when zooming out", async ({ page }) => {
  await page.goto("/en");
  await expect(onScreenCount(page)).toBeVisible();
  await expect.poll(() => numberIn(page)).toBeGreaterThan(0);
  const zoomedIn = await numberIn(page);

  // A wider view shows at least as many vehicles (two steps out also drops the area subscription).
  await zoomOut(page).click();
  await zoomOut(page).click();
  await expect.poll(() => numberIn(page)).toBeGreaterThanOrEqual(zoomedIn);
});

test("a line filter finds vehicles outside the visible area", async ({ page }) => {
  const code = await pickLineCode(page);
  await page.goto("/en");
  await expect(onScreenCount(page)).toBeVisible();

  await page.getByLabel("Route or vehicle number").fill(code);
  // Subscribed by line now: matching vehicles arrive from anywhere, not only from the visible area.
  await expect(filteredCount(page)).toBeVisible();
});
