"use client";

import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { classifyDelay } from "../model/progress";
import type { TripProgress } from "../api/progress";

type DelayBadgeProps = {
  progress: TripProgress | null | undefined;
  isPending: boolean;
  isError: boolean;
};

const tones = {
  green: "bg-green-100 text-green-900 dark:bg-green-950 dark:text-green-100",
  amber: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100",
  red: "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-100",
};

/** Small status next to the section title. Never a big error block: the section must stay usable. */
export function DelayBadge({ progress, isPending, isError }: DelayBadgeProps) {
  const t = useTranslations("route.delay");

  if (isPending) return <Skeleton className="h-5 w-16" aria-hidden />;
  if (isError && !progress) return <span className="text-xs text-muted-foreground">{t("unavailable")}</span>;
  if (!progress) return <span className="text-xs text-muted-foreground">{t("noRealtime")}</span>;

  const info = classifyDelay(progress.delaySeconds, progress.canceled);
  switch (info.kind) {
    case "none":
      return <span className="text-xs text-muted-foreground">{t("noRealtime")}</span>;
    case "canceled":
      return <Badge className={cn("border-transparent", tones.red)}>{t("canceled")}</Badge>;
    case "onTime":
      return <Badge className={cn("border-transparent", tones.green)}>{t("onTime")}</Badge>;
    case "late":
      return (
        <Badge className={cn("border-transparent", info.severity === "critical" ? tones.red : tones.amber)}>
          {t("late", { minutes: info.minutes })}
        </Badge>
      );
    case "early":
      return <Badge className={cn("border-transparent", tones.green)}>{t("early", { minutes: info.minutes })}</Badge>;
  }
}
