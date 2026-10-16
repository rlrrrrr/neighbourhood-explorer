import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import App from "./App";
import { MapView } from "./features/map-view/map-view";

describe("app shell", () => {
  it("renders the home view with navigation and theme switch", () => {
    const markup = renderToStaticMarkup(<App />);
    expect(markup).toContain("Neighbourhood Explorer");
    expect(markup).toContain("Trouve le quartier qui te ressemble.");
    expect(markup).toContain("Chat");
    expect(markup).toContain("Carte");
    expect(markup).toContain("Données");
    expect(markup).toContain("Moda");
  });

  it("offers entry points to the three views from the home", () => {
    const markup = renderToStaticMarkup(<App />);
    expect(markup).toContain("Explorer la carte");
    expect(markup).toContain("Décrire ce qu");
    expect(markup).toContain("Écris ce que tu cherches");
    expect(markup).toContain("Les quartiers colorés");
    expect(markup).toContain("Valeurs brutes, unités et sources");
  });

  it("shows the map attribution", () => {
    const markup = renderToStaticMarkup(<MapView />);
    expect(markup).toContain("OpenFreeMap");
    expect(markup).toContain("OpenStreetMap");
  });
});
