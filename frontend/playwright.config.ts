import { defineConfig } from "@playwright/test";

// End-to-end tests run against the real stack (docker compose up): live feed, database and all.
// Nothing is mocked, so they prove the pieces work together, not just in isolation.
export default defineConfig({
  testDir: "e2e",
  timeout: 90_000,
  // The tests share one live backend; running them one at a time keeps the timing predictable.
  workers: 1,
  // Fails fast when the admin account is missing; each test then signs in itself (see e2e/fixtures.ts).
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    viewport: { width: 1280, height: 800 },
    launchOptions: {
      // Set to a system browser to skip downloading Playwright's own Chromium.
      executablePath: process.env.E2E_CHROMIUM_PATH || undefined,
      // Software WebGL, so the map renders on machines and CI runners without a GPU.
      args: ["--enable-unsafe-swiftshader"],
    },
  },
});
