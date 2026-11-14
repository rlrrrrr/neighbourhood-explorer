import { useEffect, useState, type ReactNode } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { applyTheme, readStoredTheme, type ThemeName } from "./theme";
import { ThemeSwitcher } from "./theme-switcher";
import { LanguageSwitcher } from "./language-switcher";
import { Button } from "../components/ui/button";
import type { ViewName } from "./url-state";

type NavigationView = Exclude<ViewName, "home">;

const NAV_VIEWS: readonly NavigationView[] = ["chat", "map", "data"];

interface AppShellProps {
  view: ViewName;
  onNavigate: (view: ViewName) => void;
  children: ReactNode;
}

function navigationLabel(view: NavigationView): TranslatedString {
  switch (view) {
    case "chat":
      return fbt("Chat", "Navigation label for the chat view");
    case "map":
      return fbt("Carte", "Navigation label for the map view");
    case "data":
      return fbt("Données", "Navigation label for the data view");
  }
}

export function AppShell({ view, onNavigate, children }: AppShellProps) {
  const [theme, setTheme] = useState<ThemeName>(() => readStoredTheme());

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  return (
    <div className="interface-enter flex min-h-screen flex-col text-text">
      <header className="grain material-panel sticky top-0 z-10">
        <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,auto)] items-center gap-x-2 gap-y-2 px-3 py-2 sm:grid-cols-[auto_1fr_auto] sm:gap-4 sm:px-8 sm:py-3">
          <button
            type="button"
            className="col-start-1 row-start-1 flex min-h-11 cursor-pointer items-center text-sm font-extrabold tracking-tight focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action sm:min-h-9"
            onClick={() => onNavigate("home")}
          >
            <fbt desc="Product name shown in the header">Neighbourhood Explorer</fbt>
          </button>
          <nav
            aria-label={fbs("Navigation principale", "Accessibility label for the main navigation")}
            className="col-span-2 col-start-1 row-start-2 flex justify-center gap-1 sm:col-span-1 sm:col-start-2 sm:row-start-1"
          >
            {NAV_VIEWS.map((navigationView) => (
              <Button
                key={navigationView}
                variant={navigationView === view ? "primary" : "ghost"}
                size="sm"
                aria-current={navigationView === view ? "page" : undefined}
                onClick={() => onNavigate(navigationView)}
              >
                {navigationLabel(navigationView)}
              </Button>
            ))}
          </nav>
          <div className="col-start-2 row-start-1 flex min-w-0 flex-wrap items-center justify-end gap-1 sm:col-start-3 sm:flex-nowrap">
            <LanguageSwitcher />
            <ThemeSwitcher theme={theme} onChange={setTheme} />
          </div>
        </div>
      </header>
      <main className="flex flex-1 flex-col">{children}</main>
    </div>
  );
}
