"use client";

import { useEffect, type ReactNode } from "react";
import { getAccessToken, isTokenExpiring, refreshSession } from "@/entities/session";
import { env } from "@/shared/config/env";
import { VehicleSocket } from "../api/vehicle-socket";
import { useConnectionStore } from "../model/connection-store";
import { liveVehicleStore } from "../model/live-vehicle-store";

/**
 * Opens the live connection once for the whole app and closes it when the app unmounts.
 * It is only mounted for a signed-in user, so its token is always the current session's.
 */
export function LiveVehiclesProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    const socket = new VehicleSocket(
      env.vehicleSocketUrl,
      liveVehicleStore,
      useConnectionStore.getState().setStatus,
      { getToken: getAccessToken, isExpiring: isTokenExpiring, refresh: refreshSession },
    );
    socket.start();
    return () => socket.stop();
  }, []);

  return children;
}
