"use client";

import { useFormatter, useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { useConnectionStore } from "@/entities/vehicle";
import { cn } from "@/lib/utils";
import { ApiError } from "@/shared/api/http";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/shared/ui/state-views";
import { useSystemStatus, type SystemStatus } from "../api/status";
import { deriveHealth, type HealthLevel } from "../model/health";

// Each level has a symbol and text as well as a colour, so colour is never the only signal.
const healthStyles: Record<HealthLevel, { symbol: string; className: string }> = {
  healthy: { symbol: "●", className: "bg-green-100 text-green-900 dark:bg-green-950 dark:text-green-100" },
  degraded: { symbol: "▲", className: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100" },
  down: { symbol: "✕", className: "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-100" },
};

export function StatusPanel() {
  const t = useTranslations("status");
  const { data, error, isPending, isFetching, refetch, dataUpdatedAt } = useSystemStatus();

  if (isPending && !error) return <LoadingState />;
  // Error and nothing to show yet: offer a retry.
  if (error instanceof ApiError && error.status === 403) return <ForbiddenState />;
  if (!data) return <ErrorState message={error?.message ?? t("loadError")} onRetry={() => void refetch()} />;

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-4">
      <HealthBanner status={data} nowMs={dataUpdatedAt} />

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
        <RefreshedAt updatedAt={dataUpdatedAt} />
        {isFetching ? (
          <span role="status" className="flex items-center gap-1.5">
            <span className="size-3 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden />
            {t("refreshing")}
          </span>
        ) : null}
      </div>

      {/* The last data stays on screen; the warning only says it may be stale. */}
      {error ? (
        <p role="alert" className="rounded-lg bg-amber-100 p-3 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-100">
          ⚠ {t("refreshFailed")}
        </p>
      ) : null}

      {data.lastSuccessfulReadAt === null ? (
        <Card>
          <EmptyState title={t("empty.title")} hint={t("empty.hint")} />
        </Card>
      ) : null}

      <StatusGrid status={data} />
    </div>
  );
}

function RefreshedAt({ updatedAt }: { updatedAt: number }) {
  const t = useTranslations("status");
  const format = useFormatter();
  return <span>{t("refreshedAt", { time: format.dateTime(updatedAt, { timeStyle: "medium" }) })}</span>;
}

function HealthBanner({ status, nowMs }: { status: SystemStatus; nowMs: number }) {
  const t = useTranslations("status");
  const level = deriveHealth(status, nowMs);
  const style = healthStyles[level];
  return (
    <div role="status" className={cn("flex flex-col gap-1 rounded-xl p-4", style.className)}>
      <p className="text-lg font-semibold">
        <span aria-hidden>{style.symbol} </span>
        <span className="sr-only">{t("title")}: </span>
        {t(`health.${level}`)}
      </p>
      <p className="text-sm">{t(`health.${level}Hint`)}</p>
    </div>
  );
}

function StatusGrid({ status }: { status: SystemStatus }) {
  const t = useTranslations("status");
  const tConnection = useTranslations("connection");
  const format = useFormatter();
  const connection = useConnectionStore((s) => s.status);
  const dash = t("noValue");

  // Seconds are measured between two server timestamps, so a wrong browser clock cannot skew them.
  const secondsAgo =
    status.lastSuccessfulReadAt === null
      ? null
      : Math.max(0, Math.round((Date.parse(status.serverTime) - Date.parse(status.lastSuccessfulReadAt)) / 1000));

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <StatCard
        prominent
        label={t("cards.lastRead.label")}
        value={
          status.lastSuccessfulReadAt === null
            ? dash
            : format.dateTime(new Date(status.lastSuccessfulReadAt), { timeStyle: "medium" })
        }
        hint={
          secondsAgo === null ? t("cards.lastRead.none") : t("cards.lastRead.ago", { seconds: secondsAgo })
        }
      />
      <StatCard
        prominent
        label={t("cards.rowsLastHour.label")}
        value={status.rowsWrittenLastHour === null ? dash : format.number(status.rowsWrittenLastHour)}
        hint={status.rowsWrittenLastHour === null ? t("cards.rowsLastHour.unavailable") : undefined}
      />
      <StatCard
        prominent
        label={t("cards.failedReads.label")}
        value={format.number(status.failedReads)}
        hint={t("cards.failedReads.hint")}
      />
      <StatCard
        prominent
        label={t("cards.clients.label")}
        value={format.number(status.connectedClients)}
        hint={t("cards.clients.hint")}
      />

      <StatCard
        label={t("cards.duration.label")}
        value={t("cards.duration.value", { ms: format.number(status.lastReadDurationMs, { maximumFractionDigits: 0 }) })}
      />
      <StatCard label={t("cards.tracked.label")} value={format.number(status.trackedVehicles)} />
      <StatCard
        label={t("cards.entities.label")}
        value={format.number(status.lastEntityCount)}
        hint={
          <>
            {t("cards.entities.total", { count: status.lastEntityCount })}
            <br />
            {t("cards.entities.changed", { count: status.lastChangedCount })}
            <br />
            {t("cards.entities.rejected", { count: status.lastRejectedCount })}
          </>
        }
      />
      {status.tripUpdatesLastSuccessfulReadAt !== undefined ? (
        <StatCard
          label={t("cards.tripUpdatesRead.label")}
          value={
            status.tripUpdatesLastSuccessfulReadAt === null
              ? dash
              : format.dateTime(new Date(status.tripUpdatesLastSuccessfulReadAt), { timeStyle: "medium" })
          }
          hint={status.tripUpdatesLastSuccessfulReadAt === null ? t("cards.tripUpdatesRead.none") : undefined}
        />
      ) : null}
      {status.trackedTrips !== undefined ? (
        <StatCard
          label={t("cards.trackedTrips.label")}
          value={format.number(status.trackedTrips)}
          hint={t("cards.trackedTrips.hint")}
        />
      ) : null}
      <StatCard
        label={t("cards.successfulReads.label")}
        value={format.number(status.successfulReads)}
        hint={t("cards.successfulReads.hint")}
      />
      <StatCard
        label={t("cards.spool.label")}
        value={format.number(status.pendingSpoolBatches)}
        hint={t("cards.spool.hint")}
      />
      <StatCard
        label={t("cards.database.label")}
        value={status.databaseAvailable ? t("cards.database.available") : t("cards.database.unreachable")}
      />
      <StatCard label={t("cards.browser.label")} value={tConnection(connection)} hint={t("cards.browser.hint")} />
    </div>
  );
}

// A card is one labelled group: <dt> names the value in <dd>, and the hint is a second <dd>.
function StatCard({
  label,
  value,
  hint,
  prominent = false,
}: {
  label: string;
  value: string;
  hint?: ReactNode;
  prominent?: boolean;
}) {
  return (
    <Card>
      <CardContent>
        <dl>
          <dt className="text-sm text-muted-foreground">{label}</dt>
          <dd className={cn("mt-1 font-semibold tabular-nums", prominent ? "text-3xl" : "text-xl")}>{value}</dd>
          {hint ? <dd className="mt-1 text-sm text-muted-foreground">{hint}</dd> : null}
        </dl>
      </CardContent>
    </Card>
  );
}
