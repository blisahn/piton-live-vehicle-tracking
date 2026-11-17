import { describe, expect, it } from "vitest";
import { pickText, type AlertText } from "./pick-text";

const text = (value: string, language: string | null): AlertText => ({
  text: value,
  language,
  languageDetected: language !== null,
});

describe("pickText", () => {
  const both = [text("Forsinkelse", "no"), text("Delay", "en")];

  it("gives Turkish readers the English text", () => {
    expect(pickText(both, "tr")).toEqual({ text: "Delay", language: "en", isFallback: true });
  });

  it("gives English readers the English text without a fallback mark", () => {
    expect(pickText(both, "en")).toEqual({ text: "Delay", language: "en", isFallback: false });
  });

  it("falls back to Norwegian when it is the only known language", () => {
    expect(pickText([text("Forsinkelse", "no")], "tr")).toEqual({
      text: "Forsinkelse",
      language: "no",
      isFallback: true,
    });
  });

  it("uses an untagged text when nothing is tagged", () => {
    expect(pickText([text("Something", null)], "en")).toEqual({
      text: "Something",
      language: null,
      isFallback: true,
    });
  });

  it("prefers a known language over an untagged one", () => {
    expect(pickText([text("Untagged", null), text("Forsinkelse", "no")], "en")?.text).toBe("Forsinkelse");
  });

  it("returns null for no texts or only blank ones", () => {
    expect(pickText([], "tr")).toBeNull();
    expect(pickText([text("  ", "en")], "tr")).toBeNull();
  });
});
