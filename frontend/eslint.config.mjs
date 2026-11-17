import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

// Architecture rules. Dependencies only point downwards:
//
//   app  →  features  →  entities  →  shared (shared, components/ui, lib, i18n)
//
// - A feature never imports another feature; the page composes them.
// - Code outside a feature or entity reaches it only through its index.ts.
// Breaking a rule fails `npm run lint`, so the structure cannot erode unnoticed.
const restrict = (files, patterns) => ({
  files,
  rules: { "no-restricted-imports": ["error", { patterns }] },
});

const architecture = [
  restrict(
    ["src/shared/**", "src/components/**", "src/lib/**", "src/i18n/**"],
    [
      {
        group: ["@/features/**", "@/entities/**", "@/app/**"],
        message: "Shared code must not depend on entities, features or pages.",
      },
    ],
  ),
  restrict(
    ["src/entities/**"],
    [
      { group: ["@/features/**", "@/app/**"], message: "An entity must not depend on features or pages." },
      { group: ["../../*"], message: "Do not reach outside the entity with a relative path." },
    ],
  ),
  restrict(
    ["src/features/**"],
    [
      { group: ["@/features/**"], message: "A feature must not import another feature; compose them in a page." },
      { group: ["@/app/**"], message: "A feature must not depend on pages." },
      { group: ["@/entities/*/**"], message: "Import an entity through its index.ts." },
      { group: ["../../*"], message: "Do not reach outside the feature with a relative path." },
    ],
  ),
  restrict(
    ["src/app/**"],
    [
      { group: ["@/features/*/**"], message: "Import a feature through its index.ts." },
      { group: ["@/entities/*/**"], message: "Import an entity through its index.ts." },
    ],
  ),
];

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  ...architecture,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
    // Generated or copied files:
    "public/maplibre-gl-worker.mjs",
    "test-results/**",
    "playwright-report/**",
  ]),
]);

export default eslintConfig;
