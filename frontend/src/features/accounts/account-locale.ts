export type AccountLocale = "fr" | "en" | "tr" | "ar";

export function toAccountLocale(locale: string): AccountLocale | null {
  switch (locale) {
    case "fr":
    case "en":
    case "tr":
    case "ar":
      return locale;
    default:
      return null;
  }
}
