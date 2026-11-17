import type common from "../messages/en/common.json";
import type connection from "../messages/en/connection.json";
import type history from "../messages/en/history.json";
import type liveMap from "../messages/en/liveMap.json";
import type route from "../messages/en/route.json";
import type status from "../messages/en/status.json";
import type vehicle from "../messages/en/vehicle.json";
import type { routing } from "./i18n/routing";

// Makes message keys and locales type-checked: a missing or misspelled key fails the build.
declare module "next-intl" {
  interface AppConfig {
    Locale: (typeof routing.locales)[number];
    Messages: {
      common: typeof common;
      connection: typeof connection;
      liveMap: typeof liveMap;
      vehicle: typeof vehicle;
      history: typeof history;
      status: typeof status;
      route: typeof route;
    };
  }
}
