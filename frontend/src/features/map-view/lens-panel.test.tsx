import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { LensPanel } from "./lens-panel";

function renderLens({
  criteriaKeys = [],
  open = true,
}: {
  criteriaKeys?: readonly string[];
  open?: boolean;
} = {}) {
  return renderToStaticMarkup(
    <LensPanel
      criteria={{ weights: criteriaKeys.map((metricKey) => ({ metricKey, weight: 1 })) }}
      onRemoveCriterion={() => {}}
      open={open}
      onClose={() => {}}
    />,
  );
}

describe("lens panel", () => {
  it("renders the lens sections and a close control", () => {
    const markup = renderLens();

    expect(markup).toContain('aria-label="La lentille"');
    expect(markup).toContain("Istanbul");
    expect(markup).toContain("Catégories");
    expect(markup).toContain("Sélection");
    expect(markup).toContain("Fond de carte");
    expect(markup).toContain("Couches");
    expect(markup).toContain("Fermer le panneau de la lentille");
  });

  it("selects Positron as the default basemap style", () => {
    const markup = renderLens();

    expect(markup).toMatch(/<input[^>]*type="radio"[^>]*name="lens-basemap"[^>]*checked/);
    expect(markup.match(/type="radio"/g)).toHaveLength(3);
  });

  it("renders categories and layers as unchecked by default", () => {
    const markup = renderLens();

    expect(markup.match(/type="checkbox"/g)).toHaveLength(7);
    expect(markup).not.toMatch(/type="checkbox"[^>]*checked/);
  });

  it("keeps the filter controls reachable by keyboard under a styled indicator", () => {
    const markup = renderLens();

    expect(markup.match(/peer sr-only/g)).toHaveLength(10);
    expect(markup).toContain("has-[:checked]:bg-highlight");
    expect(markup).toContain("peer-checked:opacity-100");
  });

  it("shows the criteria of the shared state, with its empty state", () => {
    expect(renderLens()).toContain("Aucun critère actif");

    const markup = renderLens({ criteriaKeys: ["cafes"] });
    expect(markup).toContain("Critères");
    expect(markup).toContain("Cafés");
    expect(markup).toContain("Retirer ce critère");
    expect(markup).not.toContain("Aucun critère actif");
  });

  it("stays mounted but inert while closed, so its controls leave the tab order", () => {
    const markup = renderLens({ open: false });

    expect(markup).toContain("anchored-panel");
    expect(markup).toContain("inert");
    expect(markup).not.toContain("data-open");
  });
});
