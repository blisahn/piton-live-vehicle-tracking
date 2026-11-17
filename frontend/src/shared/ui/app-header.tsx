"use client";

import { Clock } from "lucide-react";
import { useLocale, useNow, useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { utcOffsetLabel } from "@/i18n/time-zone";
import { cn } from "@/lib/utils";

/** Top bar: app name, the two screens, a slot for live status, and the language switch. */
export function AppHeader({ status }: { status?: ReactNode }) {
  const t = useTranslations("common");
  const pathname = usePathname();
  const links = [
    { href: "/", label: t("nav.map") },
    { href: "/alerts", label: t("nav.alerts") },
    { href: "/status", label: t("nav.status") },
  ] as const;

  return (
    <header className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b px-3 py-2">
      <span className="text-sm font-semibold">{t("appName")}</span>
      <nav className="flex gap-1" aria-label={t("appName")}>
        {links.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            aria-current={pathname === link.href ? "page" : undefined}
            className={cn(
              "rounded-md px-2 py-1 text-sm hover:bg-muted",
              pathname === link.href ? "bg-muted font-medium" : "text-muted-foreground",
            )}
          >
            {link.label}
          </Link>
        ))}
      </nav>
      <div className="ml-auto flex flex-wrap items-center justify-end gap-x-3 gap-y-2">
        <TimeZoneNote />
        {status}
        <LanguageSwitch />
      </div>
    </header>
  );
}

/** Says which clock the times on screen follow, so a viewer in another country is not misled. */
function TimeZoneNote() {
  const t = useTranslations("common.timeZone");
  // Re-evaluated every minute so the offset is right across a daylight saving change.
  const offset = utcOffsetLabel(useNow({ updateInterval: 60_000 }));
  return (
    <span title={t("description", { offset })} className="inline-flex items-center gap-1 text-xs text-muted-foreground">
      <Clock className="size-3.5" aria-hidden />
      {t("label", { offset })}
    </span>
  );
}

function LanguageSwitch() {
  const t = useTranslations("common.language");
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();

  return (
    <div role="group" aria-label={t("label")} className="flex overflow-hidden rounded-md border text-xs">
      {routing.locales.map((option) => (
        <button
          key={option}
          type="button"
          lang={option}
          aria-pressed={option === locale}
          // Keeps the current page and its query string (filter, selected vehicle), only the locale changes.
          onClick={() => router.replace(`${pathname}${window.location.search}`, { locale: option })}
          className={cn("px-2 py-1 uppercase", option === locale ? "bg-foreground text-background" : "hover:bg-muted")}
        >
          <span aria-hidden>{option}</span>
          <span className="sr-only">{t(option)}</span>
        </button>
      ))}
    </div>
  );
}
