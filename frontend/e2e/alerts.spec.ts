import { expect, test } from "./fixtures";

test("the alerts screen renders its heading and count", async ({ page }) => {
  await page.goto("/en/alerts");

  await expect(page.getByRole("heading", { name: "Service alerts" })).toBeVisible();
  await expect(page.getByText(/\d+ alerts?|No alerts/).first()).toBeVisible();
});
