"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

// The three non-success states every screen has to handle. "Success" is the screen itself.

export function LoadingState({ label }: { label?: string }) {
  const t = useTranslations("common.state");
  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-2 p-4 text-sm text-muted-foreground">
      {label ?? t("loading")}
      <div aria-hidden className="flex flex-col gap-2">
        <Skeleton className="h-3 w-3/4" />
        <Skeleton className="h-3 w-1/2" />
      </div>
    </div>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: ReactNode }) {
  return (
    <div className="p-4 text-sm">
      <p className="font-medium">{title}</p>
      {hint ? <p className="mt-1 text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message?: string; onRetry?: () => void }) {
  const t = useTranslations("common.state");
  return (
    <div role="alert" className="p-4 text-sm">
      <p className="font-medium text-destructive">{t("errorTitle")}</p>
      {message ? <p className="mt-1 text-muted-foreground">{message}</p> : null}
      {onRetry ? (
        <Button variant="outline" size="sm" className="mt-3" onClick={onRetry}>
          {t("retry")}
        </Button>
      ) : null}
    </div>
  );
}

/** The signed-in user is not allowed to see this screen (403). */
export function ForbiddenState() {
  const t = useTranslations("common.state");
  return (
    <div role="alert" className="p-4 text-sm">
      <p className="font-medium">{t("forbiddenTitle")}</p>
      <p className="mt-1 text-muted-foreground">{t("forbiddenHint")}</p>
    </div>
  );
}
