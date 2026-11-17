"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";

// The three non-success states every screen has to handle. "Success" is the screen itself.

export function LoadingState({ label }: { label?: string }) {
  const t = useTranslations("common.state");
  return (
    <div role="status" aria-live="polite" className="flex items-center gap-2 p-4 text-sm text-muted-foreground">
      <span className="size-3 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden />
      {label ?? t("loading")}
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
