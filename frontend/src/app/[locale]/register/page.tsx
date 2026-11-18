import { hasLocale } from "next-intl";
import { setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { RegisterForm } from "@/features/auth";
import { routing } from "@/i18n/routing";
import { LoadingState } from "@/shared/ui/state-views";

export default async function RegisterPage({ params }: PageProps<"/[locale]/register">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);

  return (
    // The form reads ?next= from the URL, which is only known in the browser.
    <Suspense fallback={<LoadingState />}>
      <RegisterForm />
    </Suspense>
  );
}
