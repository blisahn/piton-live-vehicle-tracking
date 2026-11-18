"use client";

import { useIsAdmin } from "@/entities/session";
import { StatusPanel } from "@/features/system-status";
import { ForbiddenState } from "@/shared/ui/state-views";

/** Only admins see the status; for everyone else no request is made at all (the backend also answers 403). */
export function StatusScreen() {
  const isAdmin = useIsAdmin();
  if (!isAdmin) return <ForbiddenState />;
  return <StatusPanel />;
}
