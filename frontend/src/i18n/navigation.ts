import { createNavigation } from "next-intl/navigation";
import { routing } from "./routing";

// Locale-aware versions of Link, useRouter and usePathname: they keep the /tr or /en prefix.
export const { Link, redirect, usePathname, useRouter } = createNavigation(routing);
