import type { AnchorHTMLAttributes, ReactNode } from "react";
import { useSyncExternalStore } from "react";
import { vi } from "vitest";

/**
 * An in-memory stand-in for the browser URL. `router.replace` changes it and every component that
 * reads the query string re-renders, so a test can check what a filter does to the next request.
 */
let url = new URL("http://localhost/tr");
const listeners = new Set<() => void>();

function navigate(href: string) {
  url = new URL(href, url);
  listeners.forEach((listener) => listener());
}

/** Starts the test on this path (with query string), relative to the locale root. */
export function setUrl(pathAndQuery: string) {
  navigate(pathAndQuery);
}

export function currentUrl(): URL {
  return url;
}

export const router = {
  replace: vi.fn((href: string) => navigate(href)),
  push: vi.fn((href: string) => navigate(href)),
  back: vi.fn(),
  refresh: vi.fn(),
  prefetch: vi.fn(),
};

export function resetNavigation() {
  url = new URL("http://localhost/tr");
  Object.values(router).forEach((fn) => fn.mockClear());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function useSearch(): string {
  return useSyncExternalStore(subscribe, () => url.search);
}

function useSearchParams() {
  return new URLSearchParams(useSearch());
}

function usePathname() {
  useSearch();
  return url.pathname;
}

export const nextNavigationMock = {
  useSearchParams,
  usePathname,
  useRouter: () => router,
};

function Link({ href, children, ...rest }: { href: string; children: ReactNode } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return (
    <a href={href} {...rest}>
      {children}
    </a>
  );
}

export const i18nNavigationMock = {
  Link,
  usePathname,
  useRouter: () => router,
  redirect: vi.fn(),
};
