"use client";

import { useEffect, type ReactNode } from "react";
import { env } from "@/shared/config/env";
import { VehicleSocket } from "../api/vehicle-socket";
import { useConnectionStore } from "../model/connection-store";
import { liveVehicleStore } from "../model/live-vehicle-store";

/** Opens the live connection once for the whole app and closes it when the app unmounts. */
export function LiveVehiclesProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    const socket = new VehicleSocket(
      env.vehicleSocketUrl,
      liveVehicleStore,
      useConnectionStore.getState().setStatus,
    );
    socket.start();
    return () => socket.stop();
  }, []);

  return children;
}
