import { useEffect, useRef, useState, startTransition } from "react";
import { fbs, useLocaleContext } from "fbtee";
import { Button } from "../components/ui/button";
import { cn } from "../components/ui/cn";
import { AVAILABLE_LANGUAGES, SOURCE_LANGUAGE } from "./locale-runtime";

export function LanguageSwitcher() {
  const { locale, setLocale } = useLocaleContext();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const selectedLanguage = AVAILABLE_LANGUAGES.has(locale) ? locale : SOURCE_LANGUAGE;

  useEffect(() => {
    if (!open) {
      return;
    }
    function handleMouseDown(event: MouseEvent): void {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleMouseDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleMouseDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <Button
        variant="ghost"
        size="sm"
        aria-expanded={open}
        aria-controls="language-picker"
        onClick={() => setOpen((current) => !current)}
      >
        <span aria-hidden="true" className="font-mono text-xs">
          {selectedLanguage.toUpperCase()}
        </span>
        {AVAILABLE_LANGUAGES.get(selectedLanguage)}
      </Button>
      {open && (
        <div
          id="language-picker"
          role="group"
          aria-label={fbs(
            "Langue de l'interface",
            "Accessibility label for the language picker group",
          )}
          className="absolute end-0 z-20 mt-2 grid w-44 gap-1 rounded-2xl border border-border bg-panel-bg p-2 shadow-elevated backdrop-blur"
        >
          {[...AVAILABLE_LANGUAGES].map(([language, label]) => (
            <button
              key={language}
              type="button"
              aria-pressed={selectedLanguage === language}
              className={cn(
                "flex min-h-10 cursor-pointer items-center gap-2 rounded-xl px-3 text-left text-sm font-semibold transition-[background-color,opacity] duration-150 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action",
                selectedLanguage === language
                  ? "bg-surface ring-1 ring-action"
                  : "hover:bg-surface",
              )}
              onClick={() => {
                setOpen(false);
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
          ))}
        </div>
      )}
    </div>
  );
}
