// MapLibre runs its data processing in a Web Worker that it loads from a separate file.
// It looks for that file next to its own module, which no longer exists once Next.js has
// bundled the library. So the worker is served from /public instead, copied from the
// installed package on every dev/build run so that it always matches the library version.
import { copyFileSync, mkdirSync } from "node:fs";

mkdirSync("public", { recursive: true });
copyFileSync("node_modules/maplibre-gl/dist/maplibre-gl-worker.mjs", "public/maplibre-gl-worker.mjs");
