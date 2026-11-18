import { chromium, type FullConfig } from "@playwright/test";
import { adminCredentials } from "./fixtures";

/**
 * Signs in as the seeded admin through the real login screen once, so a missing or wrong admin
 * account fails the run immediately with a clear error instead of in every test.
 */
export default async function globalSetup(config: FullConfig) {
  const { baseURL, launchOptions } = config.projects[0].use;
  const { email, password } = adminCredentials;

  const browser = await chromium.launch(launchOptions);
  const page = await browser.newPage({ baseURL });
  await page.goto("/en/login");
  await page.getByLabel("E-mail").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Log in" }).click();
  try {
    await page.waitForURL((url) => !url.pathname.endsWith("/login"), { timeout: 15_000 });
  } catch {
    throw new Error(`Could not sign in as ${email}; set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD.`);
  } finally {
    await browser.close();
  }
}
