import { fbs } from "fbtee";
import { HeaderMenu } from "./header-menu";
import { cn } from "../components/ui/cn";
import { ALL_THEME_NAMES, THEME_DISPLAY_NAMES, type ThemeName } from "./theme";

type ThemeSwitcherProps = {
  theme: ThemeName;
  onChange: (theme: ThemeName) => void;
};

const MENU_ITEM_CLASS =
  "flex cursor-pointer items-center gap-2 rounded-xl px-2 py-1.5 text-left text-sm font-semibold transition-[background-color,transform] duration-150 ease-out motion-safe:active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action";

export function ThemeSwitcher({ theme, onChange }: ThemeSwitcherProps) {
  return (
    <HeaderMenu
      id="theme-picker"
      menuLabel={fbs("Thème de l'interface", "Accessibility label for the theme picker group")}
      panelClassName="w-64 grid-cols-2"
      trigger={
        <>
          <span aria-hidden="true" className="flex gap-0.5">
            <span className="size-2 rounded-full bg-bg ring-1 ring-border" />
            <span className="size-2 rounded-full bg-action ring-1 ring-border" />
            <span className="size-2 rounded-full bg-highlight ring-1 ring-border" />
          </span>
          {THEME_DISPLAY_NAMES[theme]}
        </>
      }
    >
      {(close) =>
        ALL_THEME_NAMES.map((option) => (
          <button
            key={option}
            type="button"
            data-theme-preview={option}
            aria-pressed={theme === option}
            className={cn(
              MENU_ITEM_CLASS,
              theme === option ? "bg-surface ring-1 ring-action" : "hover:bg-surface",
            )}
            onClick={() => {
              onChange(option);
              close();
            }}
          >
            <span aria-hidden="true" className="flex gap-0.5">
              <span className="size-3 rounded-full bg-[var(--preview-bg)] ring-1 ring-border" />
              <span className="size-3 rounded-full bg-[var(--preview-action)] ring-1 ring-border" />
              <span className="size-3 rounded-full bg-[var(--preview-highlight)] ring-1 ring-border" />
            </span>
            {THEME_DISPLAY_NAMES[option]}
          </button>
        ))
      }
    </HeaderMenu>
  );
}
