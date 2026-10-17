import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { LensPanel } from "./lens-panel";

describe("lens panel", () => {
  it("renders the lens sections and a close control", () => {
    const markup = renderToStaticMarkup(<LensPanel onClose={() => {}} />);

    expect(markup).toContain('aria-label="La lentille"');
    expect(markup).toContain("Istanbul");
    expect(markup).toContain("Catégories");
    expect(markup).toContain("Sélection");
    expect(markup).toContain("Fond de carte");
    expect(markup).toContain("Couches");
    expect(markup).toContain("Fermer le panneau de la lentille");
  });

  it("selects Positron as the default basemap style", () => {
    const markup = renderToStaticMarkup(<LensPanel onClose={() => {}} />);

    expect(markup).toMatch(/<input[^>]*type="radio"[^>]*name="lens-basemap"[^>]*checked/);
    expect(markup.match(/type="radio"/g)).toHaveLength(3);
  });

  it("renders categories and layers as unchecked by default", () => {
    const markup = renderToStaticMarkup(<LensPanel onClose={() => {}} />);

    expect(markup.match(/type="checkbox"/g)).toHaveLength(7);
    expect(markup).not.toMatch(/type="checkbox"[^>]*checked/);
  });
});
