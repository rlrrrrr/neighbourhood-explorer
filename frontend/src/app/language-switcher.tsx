import { startTransition } from "react";
import { fbs, useLocaleContext } from "fbtee";
import { HeaderMenu } from "./header-menu";
import { cn } from "../components/ui/cn";
import { AVAILABLE_LANGUAGES, SOURCE_LANGUAGE } from "./locale-runtime";

const MENU_ITEM_CLASS =
  "flex min-h-10 cursor-pointer items-center gap-2 rounded-xl px-3 text-left text-sm font-semibold transition-[background-color,transform] duration-150 ease-out motion-safe:active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action";

export function LanguageSwitcher() {
  const { locale, setLocale } = useLocaleContext();
  const selectedLanguage = AVAILABLE_LANGUAGES.has(locale) ? locale : SOURCE_LANGUAGE;

  return (
    <HeaderMenu
      id="language-picker"
      menuLabel={fbs("Langue de l'interface", "Accessibility label for the language picker group")}
      panelClassName="w-44"
      trigger={
        <>
          <span aria-hidden="true" className="font-mono text-xs">
            {selectedLanguage.toUpperCase()}
          </span>
          {AVAILABLE_LANGUAGES.get(selectedLanguage)}
        </>
      }
    >
      {(close) =>
        [...AVAILABLE_LANGUAGES].map(([language, label]) => (
          <button
            key={language}
            type="button"
            aria-pressed={selectedLanguage === language}
            className={cn(
              MENU_ITEM_CLASS,
              selectedLanguage === language ? "bg-surface ring-1 ring-action" : "hover:bg-surface",
            )}
            onClick={() => {
              close();
              startTransition(() => {
                setLocale(language);
              });
            }}
          >
            <span aria-hidden="true" className="w-6 font-mono text-xs text-text-secondary">
              {language.toUpperCase()}
            </span>
            {label}
          </button>
        ))
      }
    </HeaderMenu>
  );
}
