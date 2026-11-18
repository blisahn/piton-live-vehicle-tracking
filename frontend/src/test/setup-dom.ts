import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, vi } from "vitest";
import { resetNavigation } from "./navigation";
import { server } from "./server";

// Next.js routing is replaced by an in-memory URL, so components that read or write
// the query string behave as they would in the browser.
vi.mock("next/navigation", async () => (await import("./navigation")).nextNavigationMock);
vi.mock("@/i18n/navigation", async () => (await import("./navigation")).i18nNavigationMock);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetNavigation();
});
afterAll(() => server.close());
