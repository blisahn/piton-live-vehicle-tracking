import { hasLocale } from "next-intl";
import { setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { StatusPanel } from "@/features/system-status";
import { routing } from "@/i18n/routing";

export default async function StatusPage({ params }: PageProps<"/[locale]/status">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);
  return (
    <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6">
      <StatusPanel />
    </div>
  );
}
