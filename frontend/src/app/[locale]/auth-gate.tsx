"use client";

import { useEffect, type ReactNode } from "react";
import { safeNextPath, UserMenu, useIsAdmin, useSession } from "@/entities/session";
import { ConnectionBadge, LiveVehiclesProvider } from "@/entities/vehicle";
import { usePathname, useRouter } from "@/i18n/navigation";
import { AppHeader } from "@/shared/ui/app-header";
import { LoadingState } from "@/shared/ui/state-views";

const AUTH_PAGES = ["/login", "/register"];

/**
 * Decides what the whole app shows from the session:
 * unknown (silent refresh running) → loading; anonymous → only login/register, everything else redirects there;
 * authenticated → the app, with the live connection, and login/register redirect away.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const { status } = useSession();
  const isAdmin = useIsAdmin();
  const pathname = usePathname();
  const router = useRouter();
  const onAuthPage = AUTH_PAGES.includes(pathname);

  useEffect(() => {
    if (status === "anonymous" && !onAuthPage) {
      const current = `${pathname}${window.location.search}`;
      router.replace(current === "/" ? "/login" : `/login?next=${encodeURIComponent(current)}`);
    } else if (status === "authenticated" && onAuthPage) {
      router.replace(safeNextPath(new URLSearchParams(window.location.search).get("next")));
    }
  }, [status, onAuthPage, pathname, router]);

  if (status === "unknown") return <LoadingState />;

  if (onAuthPage) {
    if (status === "authenticated") return <LoadingState />;
    return (
      <>
        <AppHeader navigation={false} />
        <main className="flex min-h-0 flex-1 flex-col">{children}</main>
      </>
    );
  }

  if (status === "anonymous") return <LoadingState />;

  return (
    // The live connection belongs to the signed-in app, so moving between screens does not drop it
    // and logging out closes it.
    <LiveVehiclesProvider>
      <AppHeader status={<ConnectionBadge />} account={<UserMenu />} showStatusLink={isAdmin} />
      <main className="flex min-h-0 flex-1 flex-col">{children}</main>
    </LiveVehiclesProvider>
  );
}
