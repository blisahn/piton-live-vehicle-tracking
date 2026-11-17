"use client";

import { ExternalLink } from "lucide-react";
import { useFormatter, useLocale, useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { lineLabel, lineMatches, stopLabel, useLines, useStops, type Line, type Stop } from "@/entities/transit";
import { EmptyState, ErrorState, LoadingState } from "@/shared/ui/state-views";
import { useAlerts, type AlertPeriod, type ServiceAlert } from "../api/alerts";
import { pickText } from "../model/pick-text";
import { useAlertsFilter } from "../model/use-alerts-filter";

/** useStops accepts at most 200 ids per request. */
const MAX_STOP_IDS = 200;
const MAX_LINE_OPTIONS = 100;
const MAX_CHIPS = 6;
const MAX_STOPS_SHOWN = 3;
const NEW_BADGE_MS = 15_000;

const selectClass =
  "h-8 w-full min-w-0 rounded-lg border border-input bg-background px-2 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm";

export function AlertsPanel() {
  const t = useTranslations("alerts");
  const [filter, updateFilter] = useAlertsFilter();
  const { data, error, isPending, refetch } = useAlerts(filter);
  const lines = useLines();

  const alerts = useMemo(() => data?.alerts ?? [], [data]);

  const stopIds = useMemo(() => {
    const ids = new Set<string>();
    if (filter.stopId) ids.add(filter.stopId);
    for (const alert of alerts) for (const id of alert.stopIds) ids.add(id);
    return [...ids].slice(0, MAX_STOP_IDS);
  }, [alerts, filter.stopId]);
  const stops = useStops(stopIds);

  const newIds = useNewAlertIds(alerts, `${filter.lineId}|${filter.stopId}|${filter.includeInactive}`);

  const hasFilter = filter.lineId !== null || filter.stopId !== null || filter.includeInactive;

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-4">
      <h1 className="text-xl font-semibold">{t("title")}</h1>

      <Filters
        lineId={filter.lineId}
        stopId={filter.stopId}
        includeInactive={filter.includeInactive}
        lines={lines}
        stopOptions={stopIds.map((id) => ({ id, name: stopLabel(stops.get(id), id) ?? id }))}
        onChange={updateFilter}
        onClear={hasFilter ? () => updateFilter({ lineId: null, stopId: null, includeInactive: false }) : undefined}
      />

      {isPending && !error ? (
        <LoadingState />
      ) : !data ? (
        <ErrorState message={error?.message ?? t("loadError")} onRetry={() => void refetch()} />
      ) : (
        <>
          <p role="status" className="text-sm text-muted-foreground">
            {t("count", { count: alerts.length })}
          </p>
          {/* The last data stays on screen; the warning only says it may be stale. */}
          {error ? (
            <p role="alert" className="rounded-lg bg-amber-100 p-3 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-100">
              ⚠ {t("refreshFailed")}
            </p>
          ) : null}
          {alerts.length === 0 ? (
            <Card>
              <EmptyState title={t("empty.title")} hint={t("empty.hint")} />
            </Card>
          ) : (
            <ul className="flex flex-col gap-3">
              {alerts.map((alert) => (
                <li key={alert.id}>
                  <AlertCard alert={alert} lines={lines} stops={stops} isNew={newIds.has(alert.id)} />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </div>
  );
}

/**
 * Ids of alerts that were not in the previous result for the same filter; they are marked "new"
 * for a short while. The first result and a change of filter never mark anything.
 */
function useNewAlertIds(alerts: ServiceAlert[], filterKey: string): ReadonlySet<string> {
  const [tracked, setTracked] = useState<{
    alerts: ServiceAlert[];
    filterKey: string;
    ids: ReadonlySet<string>;
  } | null>(null);

  // Adjust state while rendering (React's "state from props" pattern) so there is no extra render pass of stale data.
  if (alerts.length > 0 || tracked !== null) {
    if (tracked === null || tracked.filterKey !== filterKey) {
      setTracked({ alerts, filterKey, ids: new Set() });
    } else if (tracked.alerts !== alerts) {
      const previous = new Set(tracked.alerts.map((a) => a.id));
      const fresh = alerts.filter((a) => !previous.has(a.id)).map((a) => a.id);
      setTracked({ alerts, filterKey, ids: fresh.length > 0 ? new Set(fresh) : tracked.ids });
    }
  }

  const ids = tracked?.ids;
  useEffect(() => {
    if (!ids || ids.size === 0) return;
    const timer = setTimeout(
      () => setTracked((current) => (current && current.ids === ids ? { ...current, ids: new Set() } : current)),
      NEW_BADGE_MS,
    );
    return () => clearTimeout(timer);
  }, [ids]);

  return ids ?? EMPTY_IDS;
}

const EMPTY_IDS: ReadonlySet<string> = new Set();

function Filters({
  lineId,
  stopId,
  includeInactive,
  lines,
  stopOptions,
  onChange,
  onClear,
}: {
  lineId: string | null;
  stopId: string | null;
  includeInactive: boolean;
  lines: Map<string, Line>;
  stopOptions: { id: string; name: string }[];
  onChange: (patch: { lineId?: string | null; stopId?: string | null; includeInactive?: boolean }) => void;
  onClear?: () => void;
}) {
  const t = useTranslations("alerts.filters");
  const searchId = useId();
  const lineSelectId = useId();
  const stopSelectId = useId();
  const [search, setSearch] = useState("");

  const lineOptions = useMemo(() => {
    const needle = search.trim();
    const matching = [...lines.values()]
      .filter((line) => !needle || lineMatches(line, needle))
      .map((line) => ({ id: line.id, label: lineLabel(line, line.id) ?? line.id }))
      .sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true }))
      .slice(0, MAX_LINE_OPTIONS);
    // The selected line stays selectable even when the search no longer matches it.
    if (lineId && !matching.some((o) => o.id === lineId)) {
      matching.unshift({ id: lineId, label: lineLabel(lines.get(lineId), lineId) ?? lineId });
    }
    return matching;
  }, [lines, search, lineId]);

  const sortedStops = useMemo(
    () => [...stopOptions].sort((a, b) => a.name.localeCompare(b.name)),
    [stopOptions],
  );

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <label htmlFor={searchId} className="text-sm font-medium">
              {t("lineSearch")}
            </label>
            <Input
              id={searchId}
              type="search"
              value={search}
              placeholder={t("lineSearchPlaceholder")}
              autoComplete="off"
              onChange={(event) => setSearch(event.target.value)}
            />
            <label htmlFor={lineSelectId} className="sr-only">
              {t("line")}
            </label>
            <select
              id={lineSelectId}
              className={selectClass}
              value={lineId ?? ""}
              onChange={(event) => onChange({ lineId: event.target.value || null })}
            >
              <option value="">{t("allLines")}</option>
              {lineOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
            {search.trim() && lineOptions.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("noLineMatches")}</p>
            ) : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor={stopSelectId} className="text-sm font-medium">
              {t("stop")}
            </label>
            <select
              id={stopSelectId}
              className={selectClass}
              value={stopId ?? ""}
              onChange={(event) => onChange({ stopId: event.target.value || null })}
            >
              <option value="">{t("allStops")}</option>
              {sortedStops.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-2">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4"
              checked={includeInactive}
              onChange={(event) => onChange({ includeInactive: event.target.checked })}
            />
            {t("showInactive")}
          </label>
          {onClear ? (
            <Button type="button" variant="outline" size="sm" onClick={onClear}>
              {t("clear")}
            </Button>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}

export function AlertCard({
  alert,
  lines,
  stops,
  isNew,
}: {
  alert: ServiceAlert;
  lines: Map<string, Line>;
  stops: Map<string, Stop>;
  isNew: boolean;
}) {
  const t = useTranslations("alerts");
  const locale = useLocale();
  const [expanded, setExpanded] = useState(false);
  const descriptionId = useId();

  const header = pickText(alert.header, locale);
  const description = pickText(alert.description, locale);
  const fallbackNote = (picked: ReturnType<typeof pickText>) =>
    picked?.isFallback ? t(`originalLanguage.${picked.language === "no" || picked.language === "en" ? picked.language : "unknown"}`) : null;
  const headerNote = fallbackNote(header);
  const descriptionNote = fallbackNote(description);

  const shownStops = alert.stopIds.slice(0, MAX_STOPS_SHOWN);
  const translateUrl =
    header?.isFallback || description?.isFallback ? googleTranslateUrl(header, description, locale) : null;

  return (
    <Card>
      <CardContent className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-2">
          {isNew ? <Badge>{t("new")}</Badge> : null}
          {!alert.active ? <Badge variant="outline">{t("inactive")}</Badge> : null}
        </div>

        <h2 className="break-words text-base font-semibold" lang={header?.language ?? undefined}>
          {header?.text ?? t("noHeader")}
        </h2>
        {headerNote || translateUrl ? (
          <p className="flex flex-wrap gap-x-2 text-xs text-muted-foreground">
            {headerNote ? <span>{headerNote}</span> : null}
            {translateUrl ? (
              <a href={translateUrl} target="_blank" rel="noopener noreferrer" className="underline">
                {t("translate")}
              </a>
            ) : null}
          </p>
        ) : null}

        {description ? (
          <div className="flex flex-col gap-1">
            <p
              id={descriptionId}
              className={`whitespace-pre-line break-words text-sm ${expanded ? "" : "line-clamp-3"}`}
              lang={description.language ?? undefined}
            >
              {description.text}
            </p>
            {descriptionNote && descriptionNote !== headerNote ? (
              <p className="text-xs text-muted-foreground">{descriptionNote}</p>
            ) : null}
            {description.text.length > 160 || description.text.includes("\n") ? (
              <Button
                type="button"
                variant="link"
                size="sm"
                className="self-start px-0"
                aria-expanded={expanded}
                aria-controls={descriptionId}
                onClick={() => setExpanded((v) => !v)}
              >
                {expanded ? t("less") : t("more")}
              </Button>
            ) : null}
          </div>
        ) : null}

        <Period alert={alert} />

        {alert.lineIds.length > 0 ? (
          <ul className="flex flex-wrap gap-1.5" aria-label={t("lines")}>
            {alert.lineIds.slice(0, MAX_CHIPS).map((id) => (
              <li key={id}>
                <Badge variant="secondary" className="max-w-full">
                  <span className="truncate">{lineLabel(lines.get(id), id)}</span>
                </Badge>
              </li>
            ))}
            {alert.lineIds.length > MAX_CHIPS ? (
              <li className="text-xs text-muted-foreground">{t("moreStops", { count: alert.lineIds.length - MAX_CHIPS })}</li>
            ) : null}
          </ul>
        ) : null}

        {shownStops.length > 0 ? (
          <p className="text-sm text-muted-foreground">
            <span className="font-medium">{t("stops")}:</span>{" "}
            {shownStops.map((id) => stopLabel(stops.get(id), id)).join(", ")}
            {alert.stopIds.length > shownStops.length
              ? ` ${t("moreStops", { count: alert.stopIds.length - shownStops.length })}`
              : ""}
          </p>
        ) : null}

        {alert.url ? (
          <a
            href={alert.url}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 self-start text-sm text-primary underline underline-offset-4"
          >
            {t("link")}
            {hostOf(alert.url) ? <span className="text-muted-foreground no-underline">· {hostOf(alert.url)}</span> : null}
            <ExternalLink className="size-3.5" aria-hidden />
            <span className="sr-only">{t("opensInNewTab")}</span>
          </a>
        ) : null}
      </CardContent>
    </Card>
  );
}

function Period({ alert }: { alert: ServiceAlert }) {
  const t = useTranslations("alerts.period");
  const format = useFormatter();

  if (alert.hasInvalidPeriod) {
    return <p className="text-sm text-amber-800 dark:text-amber-200">⚠ {t("invalid")}</p>;
  }
  if (alert.activePeriods.length === 0) return null;

  // The configured time zone (Europe/Oslo) applies to every date shown.
  const fmt = (iso: string) => format.dateTime(new Date(iso), { dateStyle: "medium", timeStyle: "short" });
  const describe = (period: AlertPeriod): string | null => {
    if (period.start && period.end) return t("range", { start: fmt(period.start), end: fmt(period.end) });
    if (period.start) return t("from", { start: fmt(period.start) });
    if (period.end) return t("until", { end: fmt(period.end) });
    return null;
  };
  const lines = alert.activePeriods.slice(0, 2).map(describe).filter((x): x is string => x !== null);
  if (lines.length === 0) return null;

  return (
    <p className="text-sm text-muted-foreground">
      <span className="font-medium">{t("label")}:</span> {lines.join("; ")}
    </p>
  );
}

/** We do not translate alert texts ourselves; this link opens them in Google Translate (no key, nothing sent from our backend). */
const MAX_TRANSLATE_CHARS = 2000;

function googleTranslateUrl(
  header: ReturnType<typeof pickText>,
  description: ReturnType<typeof pickText>,
  locale: string,
): string {
  const text = [header?.text, description?.text].filter(Boolean).join("\n\n").slice(0, MAX_TRANSLATE_CHARS);
  const source = header?.language ?? description?.language ?? "auto";
  const params = new URLSearchParams({ sl: source, tl: locale, text, op: "translate" });
  return `https://translate.google.com/?${params.toString()}`;
}

/** The operator's site the alert links to, so the reader sees where the link leads before clicking. */
function hostOf(url: string): string | null {
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return null;
  }
}
