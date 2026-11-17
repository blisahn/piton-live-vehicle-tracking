import { hasLocale } from "next-intl";
import { setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { routing } from "@/i18n/routing";
import { LoadingState } from "@/shared/ui/state-views";
import { LiveMapScreen } from "./live-map-screen";

export default async function LiveMapPage({ params }: PageProps<"/[locale]">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);

  return (
    // The screen reads the filter and the selected vehicle from the URL, which is only known
    // in the browser; the boundary lets the rest of the page be prerendered.
    <Suspense fallback={<LoadingState />}>
      <LiveMapScreen />
    </Suspense>
  );
}
