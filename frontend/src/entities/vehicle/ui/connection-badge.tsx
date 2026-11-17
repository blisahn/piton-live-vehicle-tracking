"use client";

import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { useConnectionStore, type ConnectionStatus } from "../model/connection-store";

const DOT_COLOR: Record<ConnectionStatus, string> = {
  connecting: "bg-amber-500 animate-pulse",
  connected: "bg-emerald-500",
  reconnecting: "bg-amber-500 animate-pulse",
  disconnected: "bg-red-500",
};

/** Always-visible connection state. Announced to screen readers when it changes. */
export function ConnectionBadge() {
  const t = useTranslations("connection");
  const status = useConnectionStore((state) => state.status);

  return (
    <span
      role="status"
      aria-live="polite"
      aria-label={`${t("label")}: ${t(status)}`}
      className="inline-flex items-center gap-2 rounded-full border px-2.5 py-1 text-xs font-medium"
    >
      <span className={cn("size-2 rounded-full", DOT_COLOR[status])} aria-hidden />
      {t(status)}
    </span>
  );
}
