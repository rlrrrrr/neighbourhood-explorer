import { useEffect, useRef, useState } from "react";
import { fbs } from "fbtee";
import { Button } from "../components/ui/button";
import { cn } from "../components/ui/cn";
import { ALL_THEME_NAMES, THEME_DISPLAY_NAMES, type ThemeName } from "./theme";

type ThemeSwitcherProps = {
  theme: ThemeName;
  onChange: (theme: ThemeName) => void;
};

export function ThemeSwitcher({ theme, onChange }: ThemeSwitcherProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

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
        aria-controls="theme-picker"
        onClick={() => setOpen((current) => !current)}
      >
        <span aria-hidden="true" className="flex gap-0.5">
          <span className="size-2 rounded-full bg-bg ring-1 ring-border" />
          <span className="size-2 rounded-full bg-action ring-1 ring-border" />
          <span className="size-2 rounded-full bg-highlight ring-1 ring-border" />
        </span>
        {THEME_DISPLAY_NAMES[theme]}
      </Button>
      {open && (
        <div
          id="theme-picker"
          role="group"
          aria-label={fbs("Thème de l'interface", "Accessibility label for the theme picker group")}
          className="absolute end-0 z-20 mt-2 grid w-64 grid-cols-2 gap-1 rounded-2xl border border-border bg-panel-bg p-2 shadow-elevated backdrop-blur"
        >
          {ALL_THEME_NAMES.map((option) => (
            <button
              key={option}
              type="button"
              data-theme-preview={option}
              aria-pressed={theme === option}
              className={cn(
                "flex cursor-pointer items-center gap-2 rounded-xl px-2 py-1.5 text-left text-sm font-semibold transition-[background-color,opacity] duration-150 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action",
                theme === option ? "bg-surface ring-1 ring-action" : "hover:bg-surface",
              )}
              onClick={() => {
                onChange(option);
                setOpen(false);
              }}
            >
              <span aria-hidden="true" className="flex gap-0.5">
                <span className="size-3 rounded-full bg-[var(--preview-bg)] ring-1 ring-border" />
                <span className="size-3 rounded-full bg-[var(--preview-action)] ring-1 ring-border" />
                <span className="size-3 rounded-full bg-[var(--preview-highlight)] ring-1 ring-border" />
              </span>
              {THEME_DISPLAY_NAMES[option]}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
