// The public surface of the vehicle entity. Features import from here, never from the files inside.
export { vehicleSchema, type Vehicle } from "./model/schemas";
export { liveVehicleStore } from "./model/live-vehicle-store";
export { useConnectionStore, type ConnectionStatus } from "./model/connection-store";
export { useLiveVehicle, useLiveVehicleCount } from "./model/use-live-vehicle";
export { useSelectedVehicle } from "./model/use-selected-vehicle";
export { LiveVehiclesProvider } from "./ui/live-vehicles-provider";
export { ConnectionBadge } from "./ui/connection-badge";
export { useAlertsVersion } from "./model/alerts-version-store";
