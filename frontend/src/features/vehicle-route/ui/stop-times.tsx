"use client";

import { useFormatter, useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { pickStopTimes, type ProgressStopLike } from "../model/progress";

/** Planned time, and the expected one next to it when it differs by a minute or more. */
export function StopTimes({ stop }: { stop: ProgressStopLike }) {
  const t = useTranslations("route.delay");
  const format = useFormatter();
  const times = pickStopTimes(stop);
  if (!times) return null;
  const clock = (iso: string) => format.dateTime(new Date(iso), { hour: "2-digit", minute: "2-digit" });

  return (
    <span className="ml-1 tabular-nums">
      {times.expected ? (
        <>
          <span className="text-muted-foreground">{clock(times.planned)}</span>
          <span aria-hidden> → </span>
          <span className="sr-only">{t("expectedAt")}</span>
          <span
            className={cn(
              "font-medium",
              times.shiftSeconds < 0
                ? "text-green-700 dark:text-green-400"
                : times.shiftSeconds >= 300
                  ? "text-red-700 dark:text-red-400"
                  : "text-amber-700 dark:text-amber-400",
            )}
          >
            {clock(times.expected)}
          </span>
        </>
      ) : (
        <span className="text-muted-foreground">{clock(times.planned)}</span>
      )}
    </span>
  );
}
