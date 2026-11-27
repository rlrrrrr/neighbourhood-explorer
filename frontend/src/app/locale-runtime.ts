import { createLocaleContext, type FbtRuntimeInput } from "fbtee";
import arabicCatalog from "../translations/ar.json";
import englishCatalog from "../translations/en.json";
import turkishCatalog from "../translations/tr.json";

export const SOURCE_LANGUAGE = "fr";

export const AVAILABLE_LANGUAGES: ReadonlyMap<string, string> = new Map([
  ["fr", "Français"],
  ["en", "English"],
  ["tr", "Türkçe"],
  ["ar", "العربية"],
]);

const LOCALE_STORAGE_KEY = "locale:v1";

function readStoredLocale(): string | null {
  try {
    return window.localStorage.getItem(LOCALE_STORAGE_KEY);
  } catch {
    // `localStorage` can throw (private browsing or disabled storage); fall back to the browser languages.
    return null;
  }
}

function readClientLocales(): ReadonlyArray<string | null> {
  if (typeof window === "undefined") {
    return navigator.languages;
  }

  const storedLocale = readStoredLocale();

  return storedLocale ? [storedLocale, ...navigator.languages] : navigator.languages;
}

export async function loadLocale(locale: string): Promise<Record<string, FbtRuntimeInput>> {
  switch (locale) {
    case "en":
      return englishCatalog.en;
    case "tr":
      return turkishCatalog.tr;
    case "ar":
      return arabicCatalog.ar;
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
  try {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Same reason: the choice still applies for the current session.
  }
}
