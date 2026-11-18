import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderOptions } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement, ReactNode } from "react";
import { loadMessages } from "@/i18n/messages";
import { DISPLAY_TIME_ZONE } from "@/i18n/time-zone";

const messages = await loadMessages("tr");

/**
 * Renders a component the way the app does: Turkish messages from the real message files,
 * Norwegian time zone, and a fresh query cache that does not retry, so an error shows at once.
 */
export function renderWithProviders(ui: ReactElement, options?: Omit<RenderOptions, "wrapper">) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  });
  function Providers({ children }: { children: ReactNode }) {
    return (
      <NextIntlClientProvider locale="tr" messages={messages} timeZone={DISPLAY_TIME_ZONE}>
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </NextIntlClientProvider>
    );
  }
  return { user: userEvent.setup(), queryClient, ...render(ui, { wrapper: Providers, ...options }) };
}
