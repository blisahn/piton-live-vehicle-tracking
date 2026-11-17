export type AlertText = {
  text: string;
  /** Normalized by the backend to "no" or "en"; null = unknown. */
  language: string | null;
  languageDetected: boolean;
};

export type PickedText = {
  text: string;
  language: string | null;
  /** True when the text is not in the viewer's language (or its language is unknown). */
  isFallback: boolean;
};

/** Order of preference per locale. Turkish never occurs in the data, so Turkish readers get English first. */
const PREFERENCE: Record<string, readonly string[]> = {
  en: ["en", "no"],
  tr: ["en", "no"],
};
const DEFAULT_PREFERENCE = ["en", "no"] as const;

/**
 * Chooses the best text for the viewer: their own language, then English, then Norwegian,
 * then texts of unknown language. Returns null when there is no (non-blank) text.
 */
export function pickText(texts: readonly AlertText[], locale: string): PickedText | null {
  const usable = texts.filter((t) => t.text.trim() !== "");
  if (usable.length === 0) return null;

  const order = PREFERENCE[locale] ?? DEFAULT_PREFERENCE;
  // The viewer's own language first, in case it is one the data does carry.
  for (const language of [locale, ...order]) {
    const match = usable.find((t) => t.language === language);
    if (match) return { text: match.text, language: match.language, isFallback: match.language !== locale };
  }
  const unknown = usable.find((t) => t.language === null) ?? usable[0];
  return { text: unknown.text, language: unknown.language, isFallback: unknown.language !== locale };
}
