import { expect, test, type Page } from "@playwright/test";

const API_URL = process.env.E2E_API_URL ?? "http://localhost:8080";

const zoomIn = (page: Page) => page.getByRole("button", { name: "Zoom in" });
const zoomOut = (page: Page) => page.getByRole("button", { name: "Zoom out" });
const countryCount = (page: Page) => page.getByText(/^[1-9][\d,.]* of [1-9][\d,.]* vehicles?$/);
const areaCount = (page: Page) => page.getByText(/^[1-9][\d,.]* vehicles? in this area$/);

/** A public code of a line that has a vehicle on the road right now. */
async function pickLineCode(page: Page): Promise<string> {
  const vehicles = (await (await page.request.get(`${API_URL}/api/vehicles`)).json()) as {
    vehicles: { routeId: string | null }[];
  };
  const lines = (await (await page.request.get(`${API_URL}/api/lines`)).json()) as {
    lines: { id: string; publicCode: string | null }[];
  };
  const active = new Set(vehicles.vehicles.map((v) => v.routeId));
  const line = lines.lines.find((l) => l.publicCode && active.has(l.id));
  expect(line, "an active vehicle should belong to a known line").toBeTruthy();
  return line!.publicCode!;
}

test("zoomed in the counter is about the visible area, zoomed out it is about the country", async ({ page }) => {
  await page.goto("/en");

  // The map opens on Oslo at zoom 9: an area subscription.
  await expect(areaCount(page)).toBeVisible();

  // Two steps out is below the area threshold: the whole country again.
  await zoomOut(page).click();
  await zoomOut(page).click();
  await expect(countryCount(page)).toBeVisible();

  await zoomIn(page).click();
  await zoomIn(page).click();
  await zoomIn(page).click();
  await expect(areaCount(page)).toBeVisible();
});

test("a line filter finds vehicles outside the visible area", async ({ page }) => {
  const code = await pickLineCode(page);
  await page.goto("/en");
  await expect(areaCount(page)).toBeVisible();

  await page.getByLabel("Route or vehicle number").fill(code);
  // Subscribed by line now, so the area no longer limits the count.
  await expect(countryCount(page)).toBeVisible();
});
