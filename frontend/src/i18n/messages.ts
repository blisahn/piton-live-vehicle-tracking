import type { AppLocale } from "./routing";

// Each feature owns one message file, so adding a screen does not mean editing a shared one.
export async function loadMessages(locale: AppLocale) {
  const [common, connection, liveMap, vehicle, history, status, route, alerts] = await Promise.all([
    import(`../../messages/${locale}/common.json`),
    import(`../../messages/${locale}/connection.json`),
    import(`../../messages/${locale}/liveMap.json`),
    import(`../../messages/${locale}/vehicle.json`),
    import(`../../messages/${locale}/history.json`),
    import(`../../messages/${locale}/status.json`),
    import(`../../messages/${locale}/route.json`),
    import(`../../messages/${locale}/alerts.json`),
  ]);
  return {
    common: common.default,
    connection: connection.default,
    liveMap: liveMap.default,
    vehicle: vehicle.default,
    history: history.default,
    status: status.default,
    route: route.default,
    alerts: alerts.default,
  };
}
