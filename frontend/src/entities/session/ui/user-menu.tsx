"use client";

import { useQueryClient } from "@tanstack/react-query";
import { LogOut } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useRouter } from "@/i18n/navigation";
import { logout } from "../api/auth";
import { useSession } from "../model/use-session";

/** The signed-in user's e-mail and the logout button, for the app header. */
export function UserMenu() {
  const t = useTranslations("auth");
  const { user } = useSession();
  const queryClient = useQueryClient();
  const router = useRouter();
  const [pending, setPending] = useState(false);

  if (!user) return null;

  async function onLogout() {
    setPending(true);
    // The store is cleared even if the call fails; the gate then unmounts the live connection.
    await logout().catch(() => undefined);
    // Nothing fetched for this user may stay around for the next one.
    queryClient.clear();
    router.replace("/login");
  }

  return (
    <div className="flex items-center gap-1">
      <span className="hidden max-w-40 truncate text-xs text-muted-foreground lg:inline" title={user.email}>
        {user.email}
      </span>
      <Button variant="ghost" size="sm" onClick={() => void onLogout()} disabled={pending} aria-label={t("logout")}>
        <LogOut aria-hidden />
        <span className="hidden sm:inline">{t("logout")}</span>
      </Button>
    </div>
  );
}
