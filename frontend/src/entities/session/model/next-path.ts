/** The page to go to after signing in: only a same-origin relative path is accepted (no open redirect). */
export function safeNextPath(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.includes("\\")) return "/";
  // The locale prefix is added by the router, so one that is already there would be doubled.
  return next.replace(/^\/(tr|en)(?=\/|$)/, "") || "/";
}
