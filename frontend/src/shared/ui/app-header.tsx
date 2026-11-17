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
    { href: "/", label: t("nav.map"), short: t("nav.mapShort") },
    { href: "/alerts", label: t("nav.alerts"), short: t("nav.alerts") },
    { href: "/status", label: t("nav.status"), short: t("nav.statusShort") },
  ] as const;

  return (
    <header className="flex items-center gap-1 border-b px-1.5 py-2 sm:gap-4 sm:px-3">
      {/* Not enough room on a phone; the nav below is labelled with it instead. */}
      <span className="hidden whitespace-nowrap text-sm font-semibold sm:inline">{t("appName")}</span>
      <nav className="flex min-w-0 gap-0 sm:gap-1" aria-label={t("appName")}>
        {links.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            aria-current={pathname === link.href ? "page" : undefined}
            className={cn(
              "whitespace-nowrap rounded-md px-1 py-1 text-xs hover:bg-muted sm:px-2 sm:text-sm",
              pathname === link.href ? "bg-muted font-medium" : "text-muted-foreground",
            )}
          >
            {/* A phone fits only the short labels next to the status and language controls. */}
            <span className="sm:hidden">{link.short}</span>
            <span className="hidden sm:inline">{link.label}</span>
          </Link>
        ))}
      </nav>
      <div className="ml-auto flex shrink-0 items-center justify-end gap-1.5 sm:gap-3">
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
      {/* Text from md up; on a phone the icon stays and the title/screen reader text carry the info. */}
      <span className="sr-only md:not-sr-only">{t("label", { offset })}</span>
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
