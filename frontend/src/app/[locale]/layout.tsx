import type { Metadata } from "next";
import { hasLocale, NextIntlClientProvider } from "next-intl";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Geist } from "next/font/google";
import { notFound } from "next/navigation";
import { ConnectionBadge, LiveVehiclesProvider } from "@/entities/vehicle";
import { routing } from "@/i18n/routing";
import { AppHeader } from "@/shared/ui/app-header";
import { QueryProvider } from "@/shared/ui/query-provider";
import "../globals.css";

const geistSans = Geist({ variable: "--font-sans", subsets: ["latin", "latin-ext"] });

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export async function generateMetadata({ params }: LayoutProps<"/[locale]">): Promise<Metadata> {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) return {};
  const t = await getTranslations({ locale, namespace: "common" });
  return { title: t("appName") };
}

export default async function LocaleLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  // Lets the pages under this layout be rendered statically for each locale.
  setRequestLocale(locale);

  return (
    <html lang={locale} className={`${geistSans.variable} h-full antialiased`}>
      <body className="flex h-full flex-col">
        <NextIntlClientProvider>
          <QueryProvider>
            {/* The live connection belongs to the layout, so moving between screens does not drop it. */}
            <LiveVehiclesProvider>
              <AppHeader status={<ConnectionBadge />} />
              <main className="flex min-h-0 flex-1 flex-col">{children}</main>
            </LiveVehiclesProvider>
          </QueryProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
