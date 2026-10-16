import { createLocaleContext, type FbtRuntimeInput } from "fbtee";

export const SOURCE_LANGUAGE = "fr";

export const AVAILABLE_LANGUAGES: ReadonlyMap<string, string> = new Map([
  ["fr", "Français"],
  ["en", "English"],
  ["tr", "Türkçe"],
  ["ar", "العربية"],
]);

const LOCALE_STORAGE_KEY = "locale";

function readClientLocales(): ReadonlyArray<string | null> {
  if (typeof window === "undefined") {
    return navigator.languages;
  }
  const storedLocale = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  return storedLocale ? [storedLocale, ...navigator.languages] : navigator.languages;
}

export async function loadLocale(locale: string): Promise<Record<string, FbtRuntimeInput>> {
  switch (locale) {
    case "en":
      return (await import("../translations/en.json")).default.en;
    case "tr":
      return (await import("../translations/tr.json")).default.tr;
    case "ar":
      return (await import("../translations/ar.json")).default.ar;
    default:
      return {};
  }
}

export const LocaleContext = createLocaleContext({
  availableLanguages: AVAILABLE_LANGUAGES,
  clientLocales: readClientLocales(),
  sourceLocale: SOURCE_LANGUAGE,
  loadLocale,
});

export function storeLocale(locale: string): void {
  window.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
}
