import { useEffect, useState } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { applyTheme, readStoredTheme, type ThemeName } from "./theme";
import { ThemeSwitcher } from "./theme-switcher";
import { LanguageSwitcher } from "./language-switcher";
import { Button } from "../components/ui/button";
import { HomeView } from "../features/home-view/home-view";
import { ChatView } from "../features/chat-view/chat-view";
import { MapView } from "../features/map-view/map-view";
import { DataView } from "../features/data-view/data-view";

type ViewName = "home" | "chat" | "map" | "data";
type NavigationView = Exclude<ViewName, "home">;

const NAV_VIEWS: readonly NavigationView[] = ["chat", "map", "data"];

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

export function AppShell() {
  const [activeView, setActiveView] = useState<ViewName>("home");
  const [theme, setTheme] = useState<ThemeName>(() => readStoredTheme());

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  return (
    <div className="flex min-h-screen flex-col text-text">
      <header className="grain sticky top-0 z-10 flex items-center gap-4 border-b border-border bg-panel-bg px-4 py-3 backdrop-blur sm:px-8">
        <button
          type="button"
          className="cursor-pointer text-sm font-extrabold tracking-tight focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          onClick={() => setActiveView("home")}
        >
          <fbt desc="Product name shown in the header">Neighbourhood Explorer</fbt>
        </button>
        <nav
          aria-label={fbs("Navigation principale", "Accessibility label for the main navigation")}
          className="flex flex-1 justify-center gap-1"
        >
          {NAV_VIEWS.map((view) => (
            <Button
              key={view}
              variant={view === activeView ? "primary" : "ghost"}
              size="sm"
              aria-current={view === activeView ? "page" : undefined}
              onClick={() => setActiveView(view)}
            >
              {navigationLabel(view)}
            </Button>
          ))}
        </nav>
        <LanguageSwitcher />
        <ThemeSwitcher theme={theme} onChange={setTheme} />
      </header>
      <main className="flex flex-1 flex-col">
        {activeView === "home" && <HomeView onNavigate={setActiveView} />}
        {activeView === "chat" && <ChatView onShowOnMap={() => setActiveView("map")} />}
        {activeView === "map" && <MapView />}
        {activeView === "data" && <DataView onShowOnMap={() => setActiveView("map")} />}
      </main>
    </div>
  );
}
