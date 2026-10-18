import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import { AppShell } from "./app-shell";
import { HomeView } from "../features/home-view/home-view";
import { MapView } from "../features/map-view/map-view";

function renderShell(view: "home" | "map") {
  return renderToStaticMarkup(
    <AppShell view={view} onNavigate={() => {}}>
      {view === "home" ? (
        <HomeView onNavigate={() => {}} />
      ) : (
        <MapView
          criteria={{ weights: [] }}
          onRemoveCriterion={() => {}}
          selectedNeighborhood={null}
          onSelectNeighborhood={() => {}}
        />
      )}
    </AppShell>,
  );
}

describe("app shell", () => {
  it("renders the home view with navigation and theme switch", () => {
    const markup = renderShell("home");

    expect(markup).toContain("Neighbourhood Explorer");
    expect(markup).toContain("Trouve le quartier qui te ressemble.");
    expect(markup).toContain("Chat");
    expect(markup).toContain("Carte");
    expect(markup).toContain("Données");
    expect(markup).toContain("Moda");
  });

  it("keeps one primary call to action and the three entry cards", () => {
    const markup = renderShell("home");

    expect(markup).toContain("Explorer la carte");
    expect(markup.match(/Explorer la carte/g)).toHaveLength(1);
    expect(markup).toContain("Écris ce que tu cherches");
    expect(markup).toContain("Les quartiers colorés");
    expect(markup).toContain("Valeurs brutes, unités et sources");
  });

  it("marks the current view in the navigation", () => {
    expect(renderShell("home").match(/aria-current="page"/g) ?? []).toHaveLength(0);

    const mapMarkup = renderShell("map");
    expect(mapMarkup.match(/aria-current="page"/g)).toHaveLength(1);
    expect(mapMarkup).toMatch(/aria-current="page"[^>]*>Carte</);
  });

  it("wraps the whole interface in the fade used when a language change remounts it", () => {
    expect(renderShell("home")).toContain(
      'class="interface-enter flex min-h-screen flex-col text-text"',
    );
  });

  it("keeps the header as a floating material rather than a hard separator", () => {
    const markup = renderShell("home");
    const header = markup.slice(markup.indexOf("<header"), markup.indexOf("</header>"));

    expect(header).toContain("material-panel");
    expect(header).not.toContain("border-b");
  });
});
