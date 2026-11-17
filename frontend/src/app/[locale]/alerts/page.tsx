import { hasLocale } from "next-intl";
import { setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { AlertsPanel } from "@/features/alerts";
import { routing } from "@/i18n/routing";
import { LoadingState } from "@/shared/ui/state-views";

export default async function AlertsPage({ params }: PageProps<"/[locale]/alerts">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);
  return (
    <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6">
      {/* The filters live in the URL, which is only known in the browser; the boundary lets the rest be prerendered. */}
      <Suspense fallback={<LoadingState />}>
        <AlertsPanel />
      </Suspense>
    </div>
  );
}
