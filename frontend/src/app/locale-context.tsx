import { useEffect, type ReactNode } from "react";
import { useLocaleContext } from "fbtee";
import { LocaleContext, storeLocale } from "./locale-runtime";

const RIGHT_TO_LEFT_LANGUAGES = new Set(["ar"]);

function LocaleDocumentAttributes() {
  const { locale } = useLocaleContext();

  useEffect(() => {
    document.documentElement.lang = locale;
    document.documentElement.dir = RIGHT_TO_LEFT_LANGUAGES.has(locale) ? "rtl" : "ltr";
    storeLocale(locale);
  }, [locale]);

  return null;
}

export function AppLocaleProvider({ children }: { children: ReactNode }) {
  return (
    <LocaleContext>
      <LocaleDocumentAttributes />
      {children}
    </LocaleContext>
  );
}
