import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  test: {
    exclude: ["e2e/**", "node_modules/**", ".next/**"],
    projects: [
      // Pure functions and stores: plain Node, no DOM.
      { extends: true, test: { name: "unit", include: ["src/**/*.test.ts"], environment: "node" } },
      // Components: a DOM, the real messages, and a fake backend (MSW) behind fetch.
      {
        extends: true,
        test: {
          name: "components",
          include: ["src/**/*.test.tsx"],
          environment: "jsdom",
          setupFiles: ["src/test/setup-dom.ts"],
        },
      },
    ],
  },
});
